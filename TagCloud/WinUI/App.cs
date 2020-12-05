﻿using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TagsCloudVisualisation;
using TagsCloudVisualisation.Layouting;
using TagsCloudVisualisation.Output;
using TagsCloudVisualisation.Text;
using TagsCloudVisualisation.Text.Formatting;
using TagsCloudVisualisation.Text.Preprocessing;
using WinUI.ImageResizing;
using WinUI.InputModels;

namespace WinUI
{
    public class App
    {
        private readonly IUi ui;
        private readonly TagCloudGenerator cloudGenerator;
        private readonly UserInputOneOptionChoice<IFileWordsReader> readerPicker;
        private readonly UserInputMultipleOptionsChoice<IWordFilter> filterPicker;
        private readonly UserInputOneOptionChoice<IWordConverter> normalizerPicker;
        private readonly UserInputOneOptionChoice<IFileResultWriter> writerPicker;
        private readonly UserInputOneOptionChoice<ILayouterFactory> layouterPicker;
        private readonly UserInputOneOptionChoice<IFontSizeResolver> fontSizeResolverPicker;
        private readonly UserInputOneOptionChoice<FontFamily> fontFamilyPicker;
        private readonly UserInputOneOptionChoice<IImageResizer> imageResizerPicker;
        private readonly UserInputField filePathInput;
        private readonly UserInputSizeField centerOffsetPicker;
        private readonly UserInputSizeField betweenWordsDistancePicker;
        private readonly UserInputSizeField imageSizePicker;
        private readonly UserInputOneOptionChoice<ImageFormat> imageFormatPicker;
        private readonly UserInputColor backgroundColorPicker;
        private readonly UserInputColorPalette colorPalettePicker;

        public App(IUi ui,
            TagCloudGenerator cloudGenerator,
            IEnumerable<IFileWordsReader> readers,
            IEnumerable<IWordFilter> filters,
            IEnumerable<IWordConverter> normalizers,
            IEnumerable<ILayouterFactory> layouters,
            IEnumerable<IFontSizeResolver> fontSources,
            IEnumerable<IFileResultWriter> writers,
            IEnumerable<IImageResizer> resizers)
        {
            this.ui = ui;
            this.cloudGenerator = cloudGenerator;

            readerPicker = UserInput.SingleChoice(ToDictionaryByName(readers), "Words file reader");
            filterPicker = UserInput.MultipleChoice(ToDictionaryByName(filters), "Words filtering method");
            writerPicker = UserInput.SingleChoice(ToDictionaryByName(writers), "Result writing method");
            layouterPicker = UserInput.SingleChoice(ToDictionaryByName(layouters), "Layouting algorithm");
            normalizerPicker = UserInput.SingleChoice(ToDictionaryByName(normalizers), "Words normalization method");
            fontSizeResolverPicker = UserInput.SingleChoice(ToDictionaryByName(fontSources), "Font size source");
            imageResizerPicker = UserInput.SingleChoice(ToDictionaryByName(resizers), "Resizing method");

            filePathInput = UserInput.Field("Enter source file path");
            fontFamilyPicker = UserInput.SingleChoice(FontFamily.Families.ToDictionary(x => x.Name), "Font family");
            centerOffsetPicker = UserInput.Size("Cloud center offset", true);
            betweenWordsDistancePicker = UserInput.Size("Minimal distance between rectangles");
            imageSizePicker = UserInput.Size("Result image size");
            backgroundColorPicker = UserInput.Color(Color.Khaki, "Image background color");
            colorPalettePicker = UserInput.ColorPalette("Words color palette", Color.DarkRed);

            var formats = new[] {ImageFormat.Gif, ImageFormat.Png, ImageFormat.Bmp, ImageFormat.Jpeg, ImageFormat.Tiff};
            imageFormatPicker = UserInput.SingleChoice(formats.ToDictionary(x => x.ToString()), "Result image format");
        }

        public void Subscribe()
        {
            ui.ExecutionRequested += ExecutionRequested;

            ui.AddUserInput(filePathInput);
            ui.AddUserInput(imageSizePicker);
            AddUserInputOrUseDefault(imageResizerPicker);
            ui.AddUserInput(backgroundColorPicker);
            ui.AddUserInput(colorPalettePicker);
            AddUserInputOrUseDefault(fontFamilyPicker);
            AddUserInputOrUseDefault(fontSizeResolverPicker);
            ui.AddUserInput(filterPicker);
            AddUserInputOrUseDefault(normalizerPicker);
            ui.AddUserInput(centerOffsetPicker);
            ui.AddUserInput(betweenWordsDistancePicker);

            AddUserInputOrUseDefault(layouterPicker);
            AddUserInputOrUseDefault(imageFormatPicker);
            AddUserInputOrUseDefault(readerPicker);
            AddUserInputOrUseDefault(writerPicker);
        }

        private async void ExecutionRequested()
        {
            using (var lockingContext = ui.StartLockingOperation())
            {
                var words = await ReadWordsAsync(filePathInput.Value, lockingContext.CancellationToken);
                if (lockingContext.CancellationToken.IsCancellationRequested)
                    return;

                var image = await CreateImageAsync(words, lockingContext.CancellationToken);

                ui.OnAfterWordDrawn(image, backgroundColorPicker.Picked);
                if (imageSizePicker.Height > 0 && imageSizePicker.Width > 0)
                {
                    var selectedResizer = imageResizerPicker.Selected.Value;
                    using (var resized = selectedResizer.Resize(image, imageSizePicker.SizeFromCurrent()))
                        FillBackgroundAndSave(resized, backgroundColorPicker.Picked);
                }
                else FillBackgroundAndSave(image, backgroundColorPicker.Picked);
            }
        }

        private async Task<Image> CreateImageAsync(WordWithFrequency[] words, CancellationToken cancellationToken)
        {
            var selectedFactory = layouterPicker.Selected.Value;
            var selectedLayouter = selectedFactory.Create(
                centerOffsetPicker.PointFromCurrent(),
                betweenWordsDistancePicker.SizeFromCurrent());

            var fontSizeSource = fontSizeResolverPicker.Selected.Value;
            var fontFamily = fontFamilyPicker.Selected.Value;

            var resultImage = await cloudGenerator.DrawWordsAsync(
                fontSizeSource,
                colorPalettePicker.PickedColors.ToArray(),
                selectedLayouter,
                words,
                cancellationToken,
                fontFamily);

            return resultImage;
        }

        private async Task<WordWithFrequency[]> ReadWordsAsync(string sourcePath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var words = readerPicker.Selected.Value.GetWordsFrom(sourcePath)
                    .Where(w => filterPicker.Selected.All(f => f.Value.IsValidWord(w)));

                var convertedWords = normalizerPicker.Selected.Value.Normalize(words);

                if (cancellationToken.IsCancellationRequested)
                    return new WordWithFrequency[0];

                var dictionary = new Dictionary<string, int>();
                foreach (var word in convertedWords)
                {
                    if (dictionary.ContainsKey(word))
                        dictionary[word] += 1;
                    else dictionary[word] = 0;

                    if (cancellationToken.IsCancellationRequested)
                        break;
                }

                return dictionary.Select(x => new WordWithFrequency(x.Key, x.Value))
                    .OrderByDescending(x => x.Frequency)
                    .ToArray();
            }, cancellationToken);
        }

        private void FillBackgroundAndSave(Image image, Color backgroundColor)
        {
            using var newImage = new Bitmap(image.Size.Width, image.Size.Height);
            using (var g = Graphics.FromImage(newImage))
            using (var brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, new Rectangle(Point.Empty, image.Size));
                g.DrawImage(image, Point.Empty);
            }

            var selectedFormat = imageFormatPicker.Selected.Value;
            writerPicker.Selected.Value.Save(newImage,
                selectedFormat,
                filePathInput.Value + "." + selectedFormat.ToString().ToLower());
        }

        private void AddUserInputOrUseDefault<T>(UserInputOneOptionChoice<T> input)
        {
            if (input.Available.Length > 1)
                ui.AddUserInput(input);
            else
                input.SetSelected(input.Available.Single().Name);
        }

        private static Dictionary<string, TService> ToDictionaryByName<TService>(IEnumerable<TService> source) =>
            source.Where(x => x != null).ToDictionary(x => VisibleName.Get(x.GetType()));
    }
}