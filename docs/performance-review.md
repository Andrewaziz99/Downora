# Performance review (Phase 9)

The question was: what happens with a big playlist, a long history and a large library? Everything below was measured on the development machine or covered by a test with a generous budget (15 seconds), so a test only fails on a real slowdown.

## Measurements

| What | Result |
| --- | --- |
| Start of the process to "database ready" (30 starts from the real log) | median 1.2 s, fastest 0.9 s; the slowest was a one-off at 20 s (cause not investigated) |
| Parsing a playlist of 5,000 entries (`ScaleTests`) | about 1 s for the whole test, including building the JSON and starting the test host |
| 20,000 jobs in the database: active queue, filtered history, status counts, statistics and a history prune (`ScaleTests`) | the whole test, including inserting the 20,000 rows, runs in about 5 s |
| A library of 10,000 files: four sort orders, a text search and a kind filter (`ScaleTests`) | the whole test, including inserting the rows, runs in about 7 s |

Indexes already cover the hot queries: `(Status, QueueOrder)` for the queue, `(NormalizedUrl, QualityPreset, Status)` for the duplicate check, `CreatedAtUtc` for history, and `AddedAtUtc`, `Title` and a unique `FilePath` for the library.

## Findings and fixes

| # | Finding | Fix |
| --- | --- | --- |
| 1 | The playlist list sat inside the page's scroll viewer, so `IsVirtualizing` did nothing: a playlist of 5,000 items built 5,000 rows and started 5,000 thumbnail downloads. | The list is a bounded (520 px), scrolling, virtualized list; only the visible rows are built. |
| 2 | The same problem for the Queue: a big playlist queues hundreds of cards, each with live progress bindings. | The Queue list is virtualized the same way. |
| 3 | The Library built a tile (with a thumbnail) for up to 500 entries at once. | It lists 120 at a time with "Show more"; changing the search, kind or sort starts again from the first page. |
| 4 | Opening the Library waited for a disk check of every entry (one `File.Exists` each, slow on a network drive) before showing anything. | The list appears first. The check runs afterwards and the list is rebuilt only if something changed. |
| 5 | A download's progress output (a line every moment) was also collected in memory. | Callers that follow the output don't buffer it. |

## Known limits

- History lists the newest 200 finished downloads. Search and the status filter reach older ones; there is no paging yet.
- Thumbnails are fetched from the web when a row first appears; nothing is cached on disk.
- Each running download is one yt-dlp process; ten at once (the maximum setting) is bounded by the machine, not by Downora.
- The UI changes in this phase (virtualization, "Show more") were not checked by eye, because the app was in use at the time; the view models and queries behind them are tested.

## Running the checks

```
dotnet test --solution Downora.sln --configuration Release
```

`ScaleTests` are ordinary tests and run with the rest. Line coverage and the coverage gate are described in the [developer guide](developer-guide.md#coverage).
