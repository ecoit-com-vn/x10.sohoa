using PdfSharpCore.Fonts;
using System.Reflection;
using System.IO;

namespace EvnHanoi.DigitizationService.Helpers
{
    public class CustomFontResolver : IFontResolver
    {
        public string DefaultFontName => "Open Sans";

        public byte[] GetFont(string faceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "EvnHanoi.DigitizationService.Fonts.OpenSans-Regular.ttf";

            if (faceName.Equals("OpenSans-Bold", StringComparison.OrdinalIgnoreCase))
            {
                resourceName = "EvnHanoi.DigitizationService.Fonts.OpenSans-Bold.ttf";
            }
            else if (faceName.Equals("OpenSans-Italic", StringComparison.OrdinalIgnoreCase))
            {
                resourceName = "EvnHanoi.DigitizationService.Fonts.OpenSans-Italic.ttf";
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Cannot find font resource {resourceName}");

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Map everything to Open Sans variants
            if (isBold && !isItalic)
            {
                return new FontResolverInfo("OpenSans-Bold");
            }
            else if (!isBold && isItalic)
            {
                return new FontResolverInfo("OpenSans-Italic");
            }
            else
            {
                return new FontResolverInfo("OpenSans-Regular");
            }
        }
    }
}
