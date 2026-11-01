// recipe: blood-watch-read
// args: none
// returns: ok, running, samples, min, max, series, error, detail
//
// Reads the `blood-watch` declaration's series: min, max and the first frames, which is what a reported jitter has to be answered
// with (a range and a period, not an impression).
((System.Func<string>)(() => AcceptanceBloodWatch.Report()))()