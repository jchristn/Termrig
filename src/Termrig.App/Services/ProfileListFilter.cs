namespace Termrig.App.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Termrig.App.Models;
    using Termrig.Core.Models;

    /// <summary>
    /// Builds visible profile rows without changing saved profiles or folder expansion.
    /// </summary>
    public static class ProfileListFilter
    {
        public static List<ProfileListItem> BuildItems(
            IReadOnlyList<TerminalProfile> profiles,
            IReadOnlyList<ProfileFolder> folders,
            string? filterText)
        {
            string query = filterText?.Trim() ?? String.Empty;
            bool filtering = query.Length > 0;
            List<TerminalProfile> matches = profiles
                .Where(profile => profile.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<ProfileListItem> items = new List<ProfileListItem>();

            foreach (TerminalProfile profile in matches.Where(profile => String.IsNullOrWhiteSpace(profile.FolderId)))
                items.Add(new ProfileListItem { Profile = profile });

            foreach (ProfileFolder folder in folders)
            {
                List<TerminalProfile> children = matches.Where(profile => profile.FolderId == folder.Id).ToList();
                if (filtering && children.Count == 0) continue;

                items.Add(new ProfileListItem { Folder = folder, IsFilterExpanded = filtering });
                if (!filtering && !folder.IsExpanded) continue;

                foreach (TerminalProfile profile in children)
                    items.Add(new ProfileListItem { Profile = profile });
            }

            return items;
        }

        /// <summary>
        /// Retains selection by ID only while the corresponding row remains visible.
        /// </summary>
        public static ProfileListItem? FindSelection(
            IReadOnlyList<ProfileListItem> items, string? profileId, string? folderId)
        {
            if (!String.IsNullOrWhiteSpace(profileId))
                return items.FirstOrDefault(item => item.Profile?.Id == profileId);
            if (!String.IsNullOrWhiteSpace(folderId))
                return items.FirstOrDefault(item => item.Folder?.Id == folderId);
            return null;
        }
    }
}
