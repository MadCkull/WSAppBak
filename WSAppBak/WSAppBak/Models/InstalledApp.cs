using Microsoft.UI.Xaml.Media.Imaging;

namespace WSAppBak.Models
{
    public class InstalledApp
    {
        public string Name { get; set; }
        public string ManifestPath { get; set; }
        public BitmapImage IconSource { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
