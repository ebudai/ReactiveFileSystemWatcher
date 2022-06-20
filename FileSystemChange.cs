// Copyright (c) 2022 Eric Budai, All Rights Reserved
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
    }
}