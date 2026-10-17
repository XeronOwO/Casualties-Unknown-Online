using System.Runtime.CompilerServices;

// The Game Adapter's own vocabulary is internal on purpose — the layer is replaceable and its
// types are not a promise to anyone outside the plugin — but the behaviour it owns is judged by
// the behavioural suite the same way the Runtime's is, and it reaches the Runtime's internals the
// same way. Without this the adapter's pure pieces (the start gate's wait wording, for one) could
// only be tested through a live game, which is why the declaration lives here beside the layer it
// opens rather than being worked around with a wider access modifier.
[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.Tests")]
