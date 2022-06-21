// Copyright (c) 2022 Eric Budai, All Rights Reserved
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Budaisoft.FileSystem
{
    /// <summary>
    ///     State of a folder (all files and subfolders, non-recursively) at a point in time
    /// </summary>
    internal class Snapshot
    {
        private readonly List<FileSystemObject> _contents = new List<FileSystemObject>();

        /// <summary>
        ///     Instantiates a snapshot for a folder
        /// </summary>
        /// <param name="folder">folder to take a snapshot of</param>
        internal Snapshot(string folder)
        {
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                _contents.Add(new FileSystemObject
                {
                    Id = FileID.GetUniqueFileID(entry),
                    Filename = entry.FullName,
                    LastWriteTime = entry.LastWriteTimeUtc
                });
            }

            _contents.Sort();
        }

        /// <summary>
        ///     Enumerates the differences between this snapshot and a more recent snapshot
        /// </summary>
        /// <param name="currentSnapshot">the other, more recent snapshot</param>
        /// <returns>a list of the changes between this and the newer snapshot</returns>
        internal List<FileSystemChange> EnumerateDifferences(Snapshot currentSnapshot)
        {
            if (_contents.Count == 0)
            {
                // we know both snapshots can't be empty, as that has no change so BufferWhenAvailable() would not return.                    
                // so, all file system objects in other are Adds
                return currentSnapshot._contents.Select(FileSystemChange.Add).ToList();
            }

            if (currentSnapshot._contents.Count == 0)
            {
                // this snapshot is not empty, more recent one is -> all items have been deleted
                return _contents.Select(FileSystemChange.Delete).ToList();
            }

            var max = Math.Max(_contents.Count, currentSnapshot._contents.Count);
            int oldSnapshotIndex = 0;
            int currentSnapshotIndex = 0;

            var changes = new List<FileSystemChange>();

            while (oldSnapshotIndex < max || currentSnapshotIndex < max)
            {
                if (oldSnapshotIndex == _contents.Count)
                {
                    // no more objects in old snapshot -> remaining objects in new snapshot are all added
                    if (currentSnapshotIndex < currentSnapshot._contents.Count)
                    {
                        var added = currentSnapshot._contents.GetRange(currentSnapshotIndex, currentSnapshot._contents.Count - currentSnapshotIndex);
                        changes.AddRange(added.Select(FileSystemChange.Add));
                    }
                    break;
                }

                if (currentSnapshotIndex == currentSnapshot._contents.Count)
                {
                    // no more objects in new snapshot -> remaining objects in old snapshot are all removed
                    if (oldSnapshotIndex < _contents.Count)
                    {
                        var removed = _contents.GetRange(oldSnapshotIndex, _contents.Count - oldSnapshotIndex);
                        changes.AddRange(removed.Select(FileSystemChange.Delete));
                    }
                    break;
                }

                var item = _contents[oldSnapshotIndex];
                var snapshotItem = currentSnapshot._contents[currentSnapshotIndex];
                var compare = item.CompareTo(snapshotItem);
                if (compare < 0)
                {
                    // item appears in this snapshot, but not in the more recent one -> the item has been deleted
                    changes.Add(FileSystemChange.Delete(item));
                    ++oldSnapshotIndex;
                }
                else if (compare > 0)
                {
                    // item does not appear in this snapshot, but is in the more recent one -> the item has been added
                    changes.Add(FileSystemChange.Add(snapshotItem));
                    ++currentSnapshotIndex;
                }
                else // compare == 0
                {
                    if (item.Filename != snapshotItem.Filename)
                    {
                        // item has been renamed (and possibly moved)
                        changes.Add(FileSystemChange.Rename(item, snapshotItem));
                    }

                    // check to see if the file has been changed within _temporalResolution (in addition to being renamed and/or moved)
                    if (snapshotItem.LastWriteTime - item.LastWriteTime > ReactiveFileSystemWatcher.Latency)
                    {
                        changes.Add(FileSystemChange.Change(snapshotItem));
                    }

                    ++oldSnapshotIndex;
                    ++currentSnapshotIndex;
                }
            }

            //changes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            return changes;
        }

        internal class FileSystemObject : IComparable<FileSystemObject>
        {
            public ulong Id { get; set; }
            public string Filename { get; set; }
            public DateTime LastWriteTime { get; set; }

            public int CompareTo(FileSystemObject other) => Id.CompareTo(other.Id);
        }
    }
}