; Shipped analyzer releases; see AnalyzerReleases.Unshipped.md for the convention.
; Recorded as shipped from the release that first carries the analyzer, rather than being staged in Unshipped
; first: these IDs go out with the first CodeBrix.Redis release as the initial set, so there is no window in
; which they are unshipped, and nothing is gained by tracking them in two places on the way. Later additions do
; go through Unshipped.
; The release number below is THIS package's. The rules were ported from StackExchange.Redis 3.1, where they
; first shipped, and keep their IDs; the version in that heading would name a CodeBrix.Redis release that does
; not exist.

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SER300  | Usage    | Warning  | TransactionAnalyzer: transaction may be replaceable by a conditional argument (any server)
SER301  | Usage    | Warning  | TransactionAnalyzer: transaction may be replaceable by a single atomic operation (newer server)
SER302  | Usage    | Warning  | TransactionAnalyzer: condition may be redundant; the queued command already reports whether it acted
SER303  | Usage    | Warning  | TransactionAnalyzer: two queued operations may be a single compound command
SER304  | Usage    | Warning  | TransactionAnalyzer: repeated queued operations may suit the variadic overload
SER350  | Build    | Warning  | AsciiHashGenerator: generated code requires a newer C# language version, so nothing was generated
