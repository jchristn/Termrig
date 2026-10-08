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

        /// <summary>
        /// Profile icon: a terminal prompt.
        /// </summary>
        public static readonly Geometry ProfileIcon = Geometry.Parse("M4 4H20C21.1 4 22 4.9 22 6V18C22 19.1 21.1 20 20 20H4C2.9 20 2 19.1 2 18V6C2 4.9 2.9 4 4 4ZM4 8V18H20V8H4ZM6 10.5L7.4 9.1L11.3 13L7.4 16.9L6 15.5L8.5 13L6 10.5ZM12 15H18V17H12V15Z");

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
        /// Row icon: a folder, or a terminal prompt for a profile.
        /// </summary>
        public Geometry IconData
        {
            get
            {
                return IsFolder ? FolderIcon : ProfileIcon;
            }
        }

        /// <summary>
        /// Row icon color.
        /// </summary>
        public IBrush IconBrush
        {
            get
            {
                return IsFolder ? FolderIconBrush : ProfileIconBrush;
            }
        }

        /// <summary>
        /// Row icon margin; profiles line their icon up with a folder's chevron.
        /// </summary>
        public Thickness IconMargin
        {
            get
            {
                return IsFolder ? new Thickness(0) : new Thickness(6, 0, 0, 0);
            }
        }

        /// <summary>
        /// True when the row shows a status badge.
        /// </summary>
        public bool HasStatus
        {
            get
            {
                return !string.IsNullOrEmpty(StatusText);
            }
        }

        /// <summary>
        /// Secondary status text shown in the profile list.
        /// </summary>
        public string StatusText
        {
            get
            {
                return Profile?.AutoOpen == true ? "auto-open" : string.Empty;
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
                    ? new Thickness(17, 0, 0, 0)
                    : new Thickness(0);
            }
        }

        #endregion

        #region Private-Members

        private static readonly IBrush FolderIconBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
        private static readonly IBrush ProfileIconBrush = new SolidColorBrush(Color.Parse("#94A3B8"));

        #endregion
    }
}
