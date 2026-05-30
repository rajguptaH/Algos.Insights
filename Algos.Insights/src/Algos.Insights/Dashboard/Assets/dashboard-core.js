const Algos = (() => {
  const state = {
    route: window.AlgosInsightsDashboard.route,
    page: window.AlgosInsightsDashboard.page
  };

  const $ = selector => document.querySelector(selector);
  const fmt = value => value ?? "";
  const number = value => new Intl.NumberFormat().format(Math.round(Number(value || 0)));
  const time = value => value ? new Date(value).toLocaleString() : "";
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#039;" }[c]));

  async function api(path) {
    const response = await fetch(state.route + path, { headers: { "Accept": "application/json" } });
    if (!response.ok) throw new Error(`Request failed (${response.status})`);
    touchLive();
    return await response.json();
  }

  function startDashboard(load) {
    document.querySelectorAll("[data-nav]").forEach(a => {
      if (a.dataset.nav === state.page) a.classList.add("active");
    });
    $("#refreshButton")?.addEventListener("click", () => load().catch(showError));
    $("#themeButton")?.addEventListener("click", toggleTheme);
    $("#closeDrawer")?.addEventListener("click", closeDrawer);
    load().catch(showError);
  }

  function touchLive() {
    const live = $("#liveStatus");
    if (live) live.textContent = `Last refreshed: ${new Date().toLocaleTimeString()}`;
  }

  function kpi(label, value, hint) {
    return `<article class="kpi"><span>${label}</span><strong>${value}</strong><small>${hint}</small></article>`;
  }

  function table(headers, rows) {
    return `<table><thead><tr>${headers.map(h => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows || `<tr><td colspan="${headers.length}">${empty("No matching telemetry yet.")}</td></tr>`}</tbody></table>`;
  }

  function statusBadge(status) {
    const cls = status >= 500 ? "bad" : status >= 400 ? "warn" : "ok";
    return `<span class="badge ${cls}">${status}</span>`;
  }

  function renderMetricList(selector, rows, labelKey, valueKey, metaKey, metaLabel) {
    const host = $(selector);
    if (!host) return;
    const max = Math.max(1, ...rows.map(x => Number(x[valueKey] || 0)));
    host.innerHTML = rows.length ? rows.map(row => {
      const width = Math.max(4, Math.round(Number(row[valueKey] || 0) * 100 / max));
      return `<div class="metric-row">
        <div class="metric-row-head"><strong title="${escapeHtml(row[labelKey])}">${escapeHtml(row[labelKey])}</strong><span>${escapeHtml(row[valueKey])}</span></div>
        <div class="bar"><i style="width:${width}%"></i></div>
        <div class="muted">${escapeHtml(metaLabel)}: ${escapeHtml(row[metaKey])}</div>
      </div>`;
    }).join("") : empty("No data yet.");
  }

  function compare(a, b, key, direction) {
    const left = a[key] ?? "";
    const right = b[key] ?? "";
    const result = typeof left === "number" && typeof right === "number"
      ? left - right
      : String(left).localeCompare(String(right));
    return direction === "desc" ? -result : result;
  }

  function short(value) {
    return value ? escapeHtml(String(value).slice(0, 12)) : "";
  }

  function bindActions() {
    document.querySelectorAll("[data-copy]").forEach(button => button.addEventListener("click", () => copy(button.dataset.copy)));
    document.querySelectorAll("[data-open]").forEach(button => button.addEventListener("click", () => openRequest(button.dataset.open)));
  }

  async function openRequest(id) {
    const detail = await api(`/api/requests/${encodeURIComponent(id)}`);
    const r = detail.request;
    $("#drawerTitle").textContent = `${r.method} ${r.path}`;
    $("#drawerBody").innerHTML = `
      <div class="detail-grid">
        ${detailBox("Status", statusBadge(r.statusCode))}
        ${detailBox("Duration", `<span class="mono">${r.durationMs}ms</span>`)}
        ${detailBox("Trace ID", copyValue(r.traceId))}
        ${detailBox("Correlation ID", copyValue(r.correlationId))}
        ${detailBox("User", escapeHtml(r.userId || "Anonymous"))}
        ${detailBox("Client IP", escapeHtml(r.clientIp || ""))}
      </div>
      ${section("Request Headers", codeBlock(r.requestHeaders))}
      ${section("Response Headers", codeBlock(r.responseHeaders))}
      ${section("Linked Exceptions", exceptionList(detail.exceptions || []))}
      ${section("Linked Dependencies", dependencyList(detail.dependencies || []))}
      ${section("Request Hierarchy", traceTree(detail.trace))}
    `;
    bindActions();
    $("#detailDrawer").classList.add("open");
    $("#detailDrawer").setAttribute("aria-hidden", "false");
  }

  function detailBox(label, value) {
    return `<div class="detail-box"><div class="tiny-label">${label}</div><div>${value}</div></div>`;
  }

  function section(title, content) {
    return `<section class="detail-box"><div class="tiny-label">${title}</div>${content}</section>`;
  }

  function codeBlock(value) {
    return `<pre>${escapeHtml(JSON.stringify(value || {}, null, 2))}</pre>`;
  }

  function exceptionList(rows) {
    return rows.length ? rows.map(x => `<div class="metric-row"><strong>${escapeHtml(x.exceptionType)}</strong><div>${escapeHtml(x.message)}</div><pre>${escapeHtml(x.stackTrace || "")}</pre></div>`).join("") : `<p class="muted">No linked exceptions.</p>`;
  }

  function dependencyList(rows) {
    return rows.length ? rows.map(x => `<div class="metric-row"><div class="metric-row-head"><strong>${escapeHtml(x.dependencyName)}</strong><span>${x.durationMs}ms</span></div><div>${escapeHtml(x.operationName)}</div></div>`).join("") : `<p class="muted">No linked dependencies.</p>`;
  }

  function traceTree(trace) {
    if (!trace || !trace.roots || !trace.roots.length) return `<p class="muted">No spans captured for this trace yet.</p>`;
    return `<div>${trace.roots.map(traceNode).join("")}</div>`;
  }

  function traceNode(node) {
    const span = node.span;
    return `<div class="trace-node"><div class="trace-card"><div class="metric-row-head"><strong>${escapeHtml(span.operationName)}</strong><span>${span.durationMs}ms</span></div><div class="mono muted">${escapeHtml(span.spanId || "")}</div></div>${(node.children || []).map(traceNode).join("")}</div>`;
  }

  function closeDrawer() {
    $("#detailDrawer")?.classList.remove("open");
    $("#detailDrawer")?.setAttribute("aria-hidden", "true");
  }

  function copyValue(value) {
    return `<button class="copy mono" data-copy="${escapeHtml(value)}">${escapeHtml(value || "")}</button>`;
  }

  async function copy(value) {
    if (!value) return;
    await navigator.clipboard.writeText(value);
    const toast = $("#toast");
    toast.textContent = "Copied";
    toast.classList.add("show");
    setTimeout(() => toast.classList.remove("show"), 1200);
  }

  function empty(message) {
    return `<div class="muted empty-state">${message}</div>`;
  }

  function skeleton() {
    return `<div class="stack-list">${Array.from({ length: 6 }).map(() => `<div class="metric-row"><div class="bar"><i style="width:65%"></i></div><div class="bar"><i style="width:35%"></i></div></div>`).join("")}</div>`;
  }

  function toggleTheme() {
    document.documentElement.dataset.theme = document.documentElement.dataset.theme === "light" ? "dark" : "light";
  }

  function showError(error) {
    const host = document.querySelector(".table-host, #traceHost, #recentRequests");
    if (host) host.innerHTML = empty(error.message);
  }

  return { $, api, bindActions, compare, empty, escapeHtml, fmt, kpi, number, renderMetricList, short, skeleton, startDashboard, statusBadge, table, time, traceTree, copy };
})();
