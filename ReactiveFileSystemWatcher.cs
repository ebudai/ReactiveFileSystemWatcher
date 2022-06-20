// Copyright (c) 2022 Eric Budai, All Rights Reserved
using Budaisoft.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Budaisoft.FileSystem
{
    public class ReactiveFileSystemWatcher : IDisposable, IObservable<List<FileSystemChange>>
    {
        public const int DEFAULT_TEMPORAL_RESOLUTION_MILLIS = 250;

        public event ErrorEventHandler Error
        {
            add { _watcher.Error += value; }
            remove { _watcher.Error -= value; }
        }

        internal string[] Ignore { get; }
        internal TimeSpan TemporalResolution { get; }

        /// <summary>
        ///     Observable list of <see cref="FileSystemChange"/>, containing all changes which occurred within the last <see cref="TemporalResolution"/> ms.
        /// </summary>
        /// <remarks>
        ///     The list will not be empty.  All changes will be distinct, and in order of their occurrence.
        /// </remarks>
        private readonly IConnectableObservable<List<FileSystemChange>> _events;

        private readonly string _root;
        private readonly FileSystemWatcher _watcher;
        private readonly IDisposable _connection;
        private readonly ConcurrentCache<string, Snapshot> _snapshots;        

        public ReactiveFileSystemWatcher(string root = @"\", string[] ignore = null, string filter = "*", bool startRunning = true, bool recurse = true, TimeSpan temporalResolution = default)
        {
            Ignore = ignore is null ? Array.Empty<string>() : ignore;
            TemporalResolution = temporalResolution == default ? TimeSpan.FromMilliseconds(DEFAULT_TEMPORAL_RESOLUTION_MILLIS) : temporalResolution;

            _snapshots = new ConcurrentCache<string, Snapshot>(folder => new Snapshot(folder, this));
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
                .BufferWhenAvailable(TemporalResolution)
                .Select(GetChanges)
                .Publish();

            _connection = _events.Connect();
        }

        private void ResetSnapshots()
        {
            _snapshots[_root] = new Snapshot(_root, this);
            foreach (var subfolder in Directory.EnumerateDirectories(_root, "*.*", SearchOption.AllDirectories))
            {
                _snapshots[subfolder] = new Snapshot(subfolder, this);
            }
        }

        public void Start()
        {
            ResetSnapshots();
            _watcher.EnableRaisingEvents = true;
        }
        
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }        

        public IDisposable Subscribe(IObserver<List<FileSystemChange>> observer) => _events.Subscribe(observer);

        [ExcludeFromCodeCoverage]
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

                // take a snapshot of the parent
                var snapshot = new Snapshot(parent, this);

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
    }

}

