using System.ComponentModel;

namespace Win10Clean
{
    /// <summary>
    /// One selectable action (remove an app, disable a service, a tweak, etc.).
    /// Each item carries the shell commands to run and which presets include it.
    /// </summary>
    public class TweakItem : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Description { get; set; }
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
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
