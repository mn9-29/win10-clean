using System.ComponentModel;

namespace WinForge
{
    /// <summary>
    /// One selectable action (remove an app, disable a service, a tweak, etc.).
    /// Each item carries the shell commands to run and which presets include it.
    /// </summary>
    public class TweakItem : INotifyPropertyChanged
    {
        private string _title;
        public string Title
        {
            get => _title;
            set { if (_title == value) return; _title = value; Raise(nameof(Title)); }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set { if (_description == value) return; _description = value; Raise(nameof(Description)); }
        }

        // The original English strings, kept so the UI can switch languages
        // back and forth without losing the source text.
        public string TitleEn { get; set; }
        public string DescEn { get; set; }

        public string Category { get; set; }
        public string[] Commands { get; set; }

        // Preset membership
        public bool Work { get; set; }
        public bool Gaming { get; set; }
        public bool Basic { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; Raise(nameof(IsSelected)); }
        }

        void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
