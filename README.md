# ReactiveFileSystemWatcher

Pros:
- Perfect accuracy via OS-supplied unique file id
- Low latency using FileSystemWatcher as a signal

Cons:
- Doesn't work across remotes (network, WSL, etc)