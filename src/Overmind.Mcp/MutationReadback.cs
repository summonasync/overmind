namespace Overmind.Mcp;

// Contract: supplying a descriptor for a mutation means the host records that mutation's logNote
// (verbatim, possibly multiline) at the tail of the log the read-back Path serves. The probe
// answers "committed" only by matching the Marker against that tail — a descriptor on a mutation
// that writes no log line would always misreport "no trace". No descriptor ⇒ honest unknown.
public sealed record MutationReadback(string Path, string Marker, string Target);
