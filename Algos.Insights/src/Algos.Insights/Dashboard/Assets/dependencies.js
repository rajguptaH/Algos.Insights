async function loadDependencies() {
  const page = await Algos.api("/api/dependencies?pageSize=500");
  const rows = page.items || [];
  const failures = rows.filter(x => !x.success).length;
  const avg = rows.length ? rows.reduce((sum, x) => sum + x.durationMs, 0) / rows.length : 0;
  Algos.$("#dependencyKpis").innerHTML = [
    Algos.kpi("Calls", Algos.number(rows.length), "Tracked dependency operations"),
    Algos.kpi("Failures", Algos.number(failures), "Failed operations"),
    Algos.kpi("Average", `${Math.round(avg)}ms`, "Mean duration"),
    Algos.kpi("Unique", Algos.number(new Set(rows.map(x => x.dependencyName)).size), "Dependency names")
  ].join("");
  Algos.$("#dependencyCount").textContent = `${rows.length} rows`;
  Algos.$("#dependencyTable").innerHTML = Algos.table(["Time", "Dependency", "Operation", "Duration", "Result", "Trace"], rows.map(row => `
    <tr>
      <td>${Algos.time(row.timestampUtc)}</td>
      <td>${Algos.escapeHtml(row.dependencyName)}</td>
      <td>${Algos.escapeHtml(row.operationName)}</td>
      <td class="mono">${row.durationMs}ms</td>
      <td>${row.success ? '<span class="badge ok">Success</span>' : '<span class="badge bad">Failed</span>'}</td>
      <td><button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button></td>
    </tr>`).join(""));
  Algos.bindActions();
}

Algos.startDashboard(loadDependencies);
