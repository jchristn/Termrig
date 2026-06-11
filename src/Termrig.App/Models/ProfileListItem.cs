namespace Termrig.App.Models
{
    using Avalonia;
    using Avalonia.Media;
    using Termrig.Core.Models;

    /// <summary>
    /// Flattened profile-list row for folder and profile entries.
    /// </summary>
    public class ProfileListItem
    {
        #region Public-Members

        /// <summary>
        /// Chevron icon for expanded folders.
        /// </summary>
        public static readonly Geometry ChevronDownIcon = Geometry.Parse("M6 9L12 15L18 9H6Z");

        /// <summary>
        /// Chevron icon for collapsed folders.
        /// </summary>
        public static readonly Geometry ChevronRightIcon = Geometry.Parse("M9 6L15 12L9 18V6Z");

        /// <summary>
        /// Folder icon.
        /// </summary>
        public static readonly Geometry FolderIcon = Geometry.Parse("M3 6C3 4.895 3.895 4 5 4H10L12 6H19C20.105 6 21 6.895 21 8V18C21 19.105 20.105 20 19 20H5C3.895 20 3 19.105 3 18V6Z");

        #endregion

        #region Public-Members

        /// <summary>
        /// Folder row, when this item represents a folder.
        /// </summary>
        public ProfileFolder? Folder { get; set; }

        /// <summary>
        /// Profile row, when this item represents a profile.
        /// </summary>
        public TerminalProfile? Profile { get; set; }

        /// <summary>
        /// True when this row represents a folder.
        /// </summary>
        public bool IsFolder
        {
            get
            {
                return Folder != null;
            }
        }

        /// <summary>
        /// Text shown in the profile list.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Folder != null) return Folder.Name;
                return Profile?.Name ?? string.Empty;
            }
        }

        /// <summary>
        /// Folder expansion icon.
        /// </summary>
        public Geometry ExpandIconData
        {
            get
            {
                return Folder?.IsExpanded == true ? ChevronDownIcon : ChevronRightIcon;
            }
        }

        /// <summary>
        /// Folder row icon.
        /// </summary>
        public Geometry FolderIconData
        {
            get
            {
                return FolderIcon;
            }
        }

        /// <summary>
        /// Secondary status text shown in the profile list.
        /// </summary>
        public string StatusText
        {
            get
            {
                return Profile?.AutoOpen == true ? "auto" : string.Empty;
            }
        }

        /// <summary>
        /// Row text weight.
        /// </summary>
        public FontWeight RowFontWeight
        {
            get
            {
                return IsFolder ? FontWeight.SemiBold : FontWeight.Normal;
            }
        }

        /// <summary>
        /// Row margin.
        /// </summary>
        public Thickness RowMargin
        {
            get
            {
                return Profile != null && !string.IsNullOrWhiteSpace(Profile.FolderId)
                    ? new Thickness(18, 0, 0, 0)
                    : new Thickness(0);
            }
        }

        /// <summary>
        /// Row background brush.
        /// </summary>
        public IBrush RowBackground
        {
            get
            {
                return IsFolder ? FolderRowBackground : Brushes.Transparent;
            }
        }

        /// <summary>
        /// Row border brush.
        /// </summary>
        public IBrush RowBorderBrush
        {
            get
            {
                return IsFolder ? FolderRowBorder : Brushes.Transparent;
            }
        }

        #endregion

        #region Private-Members

        private static readonly IBrush FolderRowBackground = new SolidColorBrush(Color.Parse("#182632"));
        private static readonly IBrush FolderRowBorder = new SolidColorBrush(Color.Parse("#314251"));

        #endregion
    }
}
