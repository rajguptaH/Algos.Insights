async function loadTrace() {
  const traceId = Algos.$("#traceInput").value.trim();
  if (!traceId) {
    Algos.$("#traceHost").innerHTML = Algos.empty("Paste a Trace ID to inspect the hierarchy.");
    return;
  }

  const trace = await Algos.api(`/api/traces/${encodeURIComponent(traceId)}`);
  Algos.$("#traceHost").innerHTML = Algos.traceTree(trace);
}

Algos.$("#loadTraceButton").addEventListener("click", () => loadTrace().catch(error => Algos.$("#traceHost").innerHTML = Algos.empty(error.message)));
Algos.$("#copyTraceButton").addEventListener("click", () => Algos.copy(Algos.$("#traceInput").value.trim()));
Algos.$("#traceInput").addEventListener("keydown", event => {
  if (event.key === "Enter") loadTrace().catch(error => Algos.$("#traceHost").innerHTML = Algos.empty(error.message));
});
Algos.startDashboard(loadTrace);
