// Copyright (c) 2022 Eric Budai, All Rights Reserved
using static Budaisoft.FileSystem.Snapshot;

namespace Budaisoft.FileSystem
{
    /// <summary>
    ///     Represents a change in the file system
    /// </summary>
    public struct FileSystemChange
    {
        public enum ChangeTypes { Add, Delete, Change, Rename };

        public ChangeTypes ChangeType;
        public string FullName;
        public string OldName; // used for rename only

        internal static FileSystemChange Added(FileSystemObject item) => new FileSystemChange() { ChangeType = ChangeTypes.Add, FullName = item.Filename };
        internal static FileSystemChange Deleted(FileSystemObject item) => new FileSystemChange() { ChangeType = ChangeTypes.Delete, FullName = item.Filename };
        internal static FileSystemChange Changed(FileSystemObject item) => new FileSystemChange() { ChangeType = ChangeTypes.Change, FullName = item.Filename };
        internal static FileSystemChange Renamed(FileSystemObject from, FileSystemObject to) => new FileSystemChange() 
        {
            ChangeType = ChangeTypes.Rename, 
            FullName = to.Filename, 
            OldName = from.Filename 
        };
    }
}