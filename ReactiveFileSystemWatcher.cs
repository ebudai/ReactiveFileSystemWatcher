// Copyright (c) 2022 Eric Budai, All Rights Reserved
using Budaisoft.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Budaisoft.FileSystem
{
    public class ReactiveFileSystemWatcher : IDisposable, IObservable<List<FileSystemChange>>
    {
        /// <summary>
        ///     Timespan over which changes are buffered into a list.
        /// </summary>
        /// <remarks>
        ///     Experments show that setting this to a value under 25ms will produce dropped events.  DO NOT DO.
        /// </remarks>
        public static TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_LATENCY_MILLIS);

        /// <summary>
        ///     Error event.  Wraps <see cref="_watcher"/>.<see cref="Error"/>
        /// </summary>
        public event ErrorEventHandler Error
        {
            add { _watcher.Error += value; }
            remove { _watcher.Error -= value; }
        }

        /// <summary>
        ///     List of files/subfolders to ignore.  Changes to these will not fire an event.
        /// </summary>
        /// <remarks>
        ///     Wildcards don't work.
        /// </remarks>
        internal string[] IgnoredFolders { get; }

        /// <summary>
        ///     Default value for temporal resolution.  All changes within this timeframe will be published as one list.
        /// </summary>
        private const int DEFAULT_LATENCY_MILLIS = 50;

        /// <summary>
        ///     Observable list of <see cref="FileSystemChange"/>, containing all changes which occurred within the last <see cref="Latency"/> ms.
        /// </summary>
        /// <remarks>
        ///     The list will not be empty.  All changes will be distinct, and in order of their occurrence.
        /// </remarks>
        private readonly IConnectableObservable<List<FileSystemChange>> _events;

        /// <summary>
        ///     Top-level folder to watch.
        /// </summary>
        private readonly string _root;

        /// <summary>
        ///     Wrapped <see cref="FileSystemWatcher"/>.
        /// </summary>
        private readonly FileSystemWatcher _watcher;

        /// <summary>
        ///     Reference to <see cref="_events"/> connection for disposal.
        /// </summary>
        private readonly IDisposable _connection;

        /// <summary>
        ///     Existing filesystem state.
        /// </summary>
        private readonly ConcurrentCache<string, Snapshot> _snapshots;        

        /// <summary>
        ///     Initializes a new instance of the <see cref="ReactiveFileSystemWatcher"/> class.
        /// </summary>
        /// <param name="root">Top-level folder name to watch.  Defaults to current folder.</param>
        /// <param name="ignoredFolders">Files/folders to ignore changes of.  Defaults to none.</param>
        /// <param name="filter">Limit watching to this filter. Defaults to "*".</param>
        /// <param name="startRunning">Whether or not to listen immediately.  Defaults to true.</param>
        /// <param name="recurse">Whether or not to listen for changes inside subfolders.  Defaults to true.</param>
        /// <remarks>
        ///     For this to do anything, you must <see cref="Subscribe(IObserver{List{FileSystemChange}})"/> to the instance.
        /// </remarks>
        public ReactiveFileSystemWatcher(string root = @"\", string[] ignoredFolders = null, string filter = "*", bool startRunning = true, bool recurse = true)
        {
            IgnoredFolders = ignoredFolders ?? Array.Empty<string>();

            _snapshots = new ConcurrentCache<string, Snapshot>(folder => new Snapshot(folder));
            _root = root;

            _watcher = new FileSystemWatcher(_root)
            {
                Filter = filter,
                EnableRaisingEvents = startRunning,
                IncludeSubdirectories = recurse,
                NotifyFilter = NotifyFilters.DirectoryName
                    | NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime
                    | NotifyFilters.LastAccess
                    | NotifyFilters.Size
                // NotifyFilters.Attributes has no effect so is unused
                // NotifyFilters.Security is unsupported
            };

            _watcher.Error += (sender, e) =>
            {
                var watcher = sender as FileSystemWatcher;
                // FileSystemWatcher will stop watching when an exception occurs. re-enable unless folder was deleted.
                watcher.EnableRaisingEvents = Directory.Exists(watcher.Path);
            };

            // produce snapshots for this and all subfolders
            ResetSnapshots();

            // build Events observable
            var changed = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => _watcher.Changed += h, h => _watcher.Changed -= h).Select(e => e.EventArgs.FullPath);
            var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>      (h => _watcher.Renamed += h, h => _watcher.Renamed -= h).Select(e => e.EventArgs.FullPath);
            var deleted = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => _watcher.Deleted += h, h => _watcher.Deleted -= h).Select(e => e.EventArgs.FullPath);
            var created = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => _watcher.Created += h, h => _watcher.Created -= h).Select(e => e.EventArgs.FullPath);

            _events = Observable.Merge(changed, renamed, deleted, created)
                .Distinct()
                .BufferWhenAvailable(Latency)
                .Select(GetChanges)
                .Publish();

            _connection = _events.Connect();
        }

        /// <summary>
        ///     Resets internal state.
        /// </summary>
        private void ResetSnapshots()
        {
            _snapshots[_root] = new Snapshot(_root);
            foreach (var subfolder in Directory.EnumerateDirectories(_root, "*.*", SearchOption.AllDirectories))
            {
                if (IsIgnored(subfolder)) continue;
                _snapshots[subfolder] = new Snapshot(subfolder);
            }
        }

        /// <summary>
        ///     Starts sending file system change events.
        /// </summary>
        /// <remarks>
        ///     Changes which occurred before Start() was called will not be sent.
        /// </remarks>
        public void Start()
        {
            ResetSnapshots();
            _watcher.EnableRaisingEvents = true;
        }
        
        /// <summary>
        ///     Stops sending file system change events.
        /// </summary>
        /// <remarks>
        ///     
        /// </remarks>
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        /// <summary>
        ///     Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <param name="observer">The object that is to receive notifications.</param>
        /// <returns>
        ///     A reference to an interface that allows observers to stop receiving notifications before the provider has finished sending them.
        /// </returns>
        public IDisposable Subscribe(IObserver<List<FileSystemChange>> observer) => _events.Subscribe(observer);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _watcher.Dispose();
            _connection.Dispose();
        }

        /// <summary>
        ///     Produces a list of <see cref="FileSystemChange"/> objects given a list of changed filesystem paths.
        /// </summary>
        /// <param name="paths">A list of filesystem paths representing objects which have changed</param>
        /// <returns>A non-empty list of distinct <see cref="FileSystemChange"/> objects</returns>
        private List<FileSystemChange> GetChanges(IList<string> paths)
        {
            var changes = new List<FileSystemChange>();

            foreach (var path in paths)
            {
                // get path's parent folder
                string parent = Path.GetDirectoryName(path);
                if (!Directory.Exists(parent))
                {
                    // path's parent folder no longer exists
                    changes.Add(new FileSystemChange { ChangeType = FileSystemChange.ChangeTypes.Delete, FullName = path });
                    _snapshots.TryRemove(parent, out _);
                    continue;
                }

                // bail if this is an ignored folder
                if (IsIgnored(path)) continue;

                // take a snapshot of the parent
                var snapshot = new Snapshot(parent);

                // enumerate the differences between the snapshots
                var diff = _snapshots[parent].EnumerateDifferences(snapshot);

                // if there are differences, add them to the return value list and update _snapshots with the current snapshot
                if (diff.Count > 0)
                {
                    changes.AddRange(diff);
                    _snapshots[parent] = snapshot;
                }
            }

            return changes;
        }

        private bool IsIgnored(string path)
        {
            var folder = new DirectoryInfo(path);
            var relativePath = folder.FullName.Substring(_root.Length);
            var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var ignored in IgnoredFolders)
            {
                if (parts.Contains(ignored)) return true;
            }
            return false;
        }
    }

}

