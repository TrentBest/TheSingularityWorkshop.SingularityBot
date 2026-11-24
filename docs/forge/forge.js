(() => {
  "use strict";
  const $ = (id) => document.getElementById(id);
  const editor = $("definition-json");
  const state = { lastValid: null, dirty: false };
  const sampleUrl = "../examples/bot-definitions/terran-adaptive-marine.v1.json";

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, ch => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" }[ch]));
  }
  function parse() {
    try { return { value: JSON.parse(editor.value), error: null }; }
    catch (error) { return { value: null, error: error.message }; }
  }
  function validateDefinition(bot) {
    const errors = [];
    if (!bot || typeof bot !== "object" || Array.isArray(bot)) return ["$ must be a JSON object."];
    const required = ["schemaVersion","id","name","race","modeId","raceSelection","strategyProfile","runtime","opening","defaultGoals","strategyTransitions","ai"];
    for (const key of required) if (!(key in bot)) errors.push("$." + key + " is required.");
    for (const key of Object.keys(bot)) if (!required.includes(key)) errors.push("$." + key + " is not part of the v1 authoring contract.");
    if (bot.schemaVersion !== 1) errors.push("$.schemaVersion must be 1.");
    if (typeof bot.id !== "string" || !bot.id.trim() || bot.id.length > 128) errors.push("$.id must contain 1–128 characters.");
    if (typeof bot.name !== "string" || !bot.name.trim() || bot.name.length > 160) errors.push("$.name must contain 1–160 characters.");
    const raceModes = { Terran: "terran.marine.v1", Zerg: "zerg.zergling.v1", Protoss: "protoss.zealot.v1" };
    if (!Object.prototype.hasOwnProperty.call(raceModes, bot.race)) errors.push("$.race must be Terran, Zerg, or Protoss.");
    if (bot.modeId && raceModes[bot.race] !== bot.modeId) errors.push("$.modeId must match the selected race (" + (raceModes[bot.race] || "no registered mode") + ").");
    if (bot.raceSelection && (bot.raceSelection.policy !== "fixed" ||
        bot.raceSelection.fixedRace !== String(bot.race).toLowerCase() ||
        !Array.isArray(bot.raceSelection.eligibleRaces) ||
        bot.raceSelection.eligibleRaces.length !== 1 ||
        bot.raceSelection.eligibleRaces[0] !== String(bot.race).toLowerCase())) {
      errors.push("$.raceSelection must declare the selected race as a fixed, single-race allow-list.");
    }
    if (typeof bot.strategyProfile !== "string" || !bot.strategyProfile.trim()) errors.push("$.strategyProfile is required.");
    const runtime = bot.runtime;
    if (!runtime || runtime.schemaVersion !== 1 || !runtime.bot || !Array.isArray(runtime.advisors)) errors.push("$.runtime must contain schemaVersion 1, bot settings, and advisors.");
    else {
      if (runtime.bot.race !== bot.race) errors.push("$.runtime.bot.race must match $.race.");
      if (runtime.bot.strategyProfile !== bot.strategyProfile) errors.push("$.runtime.bot.strategyProfile must match $.strategyProfile.");
      const ids = runtime.advisors.map(x => x && x.id);
      if (ids.some(x => typeof x !== "string" || !x) || new Set(ids).size !== ids.length) errors.push("$.runtime.advisors must have unique non-empty ids.");
    }
    const opening = bot.opening;
    if (!opening || opening.schemaVersion !== "0.1" || opening.race !== bot.race || !Array.isArray(opening.steps) || opening.steps.length < 1 || opening.steps.length > 256) errors.push("$.opening must be a v0.1 Terran production plan with 1–256 steps.");
    else {
      const ids = opening.steps.map(x => x && x.id);
      if (ids.some(x => typeof x !== "string" || !x) || new Set(ids).size !== ids.length) errors.push("$.opening.steps must have unique non-empty ids.");
      opening.steps.forEach((step, i) => {
        const p = "$.opening.steps[" + i + "]";
        if (!step || typeof step !== "object") { errors.push(p + " must be an object."); return; }
        for (const key of ["target","producer"]) if (typeof step[key] !== "string" || !step[key].trim()) errors.push(p + "." + key + " is required.");
        if (!Number.isInteger(step.desiredCount) || step.desiredCount < 1) errors.push(p + ".desiredCount must be a positive integer.");
        if (!Number.isInteger(step.earliestGameLoop) || step.earliestGameLoop < 0) errors.push(p + ".earliestGameLoop must be a non-negative integer.");
        if (!Number.isInteger(step.priority) || step.priority < 0 || step.priority > 100) errors.push(p + ".priority must be an integer from 0 to 100.");
        if (step.lane !== undefined && !["buildings","units"].includes(step.lane)) errors.push(p + ".lane must be buildings or units.");
      });
    }
    const goals = bot.defaultGoals;
    if (!goals || goals.version !== 1 || !["none","rally","scout","contain","engage","fallback"].includes(goals.defaultRole)) errors.push("$.defaultGoals must use version 1 and a supported defaultRole.");
    else if (!Number.isInteger(goals.scoutCount) || goals.scoutCount < 0 || typeof goals.stutterStepEnabled !== "boolean" || typeof goals.engageVisibleThreats !== "boolean" || typeof goals.excludeEnemyStartSector !== "boolean" || typeof goals.fallbackHealthFraction !== "number" || goals.fallbackHealthFraction < 0 || goals.fallbackHealthFraction > 1 || !Array.isArray(goals.scoutSectors)) errors.push("$.defaultGoals contains invalid goal settings.");
    if (!Array.isArray(bot.strategyTransitions) || bot.strategyTransitions.length > 64) errors.push("$.strategyTransitions must be an array of at most 64 transitions.");
    const ai = bot.ai;
    if (!ai || typeof ai.enabled !== "boolean" || !Number.isInteger(ai.maxOnStartCalls) || ai.maxOnStartCalls < 0 || ai.maxOnStartCalls > 1 || !Number.isInteger(ai.maxInMatchCalls) || ai.maxInMatchCalls < 0 || ai.maxInMatchCalls > 3 || !Number.isInteger(ai.timeoutMilliseconds) || ai.timeoutMilliseconds < 1000 || ai.timeoutMilliseconds > 120000 || !Number.isInteger(ai.minimumGameLoopSpacing) || ai.minimumGameLoopSpacing < 0 || !Array.isArray(ai.allowedTriggers) || ai.allowedTriggers.length > 8) errors.push("$.ai contains invalid or out-of-range policy values.");
    else {
      if (ai.enabled && (!String(ai.provider || "").trim() || !String(ai.model || "").trim())) errors.push("$.ai requires provider and model when enabled.");
      if (!ai.enabled && (ai.maxOnStartCalls !== 0 || ai.maxInMatchCalls !== 0)) errors.push("$.ai must set both call budgets to zero when disabled.");
      if (new Set(ai.allowedTriggers).size !== ai.allowedTriggers.length || ai.allowedTriggers.some(x => typeof x !== "string" || !x.trim())) errors.push("$.ai.allowedTriggers must contain unique non-empty strings.");
      if (ai.enabled && ai.maxInMatchCalls > 0 && ai.allowedTriggers.length === 0) errors.push("$.ai.allowedTriggers needs at least one trigger when in-match calls are enabled.");
    }
    return errors;
  }
  const raceProfiles = {
    Terran: {
      modeId: "terran.marine.v1",
      strategyProfile: "terran.marine-economy.v1"
    },
    Zerg: {
      modeId: "zerg.zergling.v1",
      strategyProfile: "zerg.zergling-pressure.v1",
      opening: { id: "zerg.first-zergling", name: "First Zergling pair",
        steps: [{ id: "zergling-pair", target: "Zergling", producer: "Larva", desiredCount: 2, earliestGameLoop: 0, priority: 100, lane: "units" }] }
    },
    Protoss: {
      modeId: "protoss.zealot.v1",
      strategyProfile: "protoss.zealot-pressure.v1",
      opening: { id: "protoss.first-zealot", name: "Pylon, Gateway, first Zealot",
        steps: [{ id: "first-zealot", target: "Zealot", producer: "Gateway", desiredCount: 1, earliestGameLoop: 0, priority: 100, lane: "units" }] }
    }
  };
  function applyRace(bot, race) {
    const profile = raceProfiles[race];
    if (!profile) return;
    bot.race = race;
    bot.modeId = profile.modeId;
    bot.strategyProfile = profile.strategyProfile;
    bot.raceSelection = { policy: "fixed", fixedRace: race.toLowerCase(), eligibleRaces: [race.toLowerCase()] };
    if (bot.runtime?.bot) {
      bot.runtime.bot.race = race;
      bot.runtime.bot.strategyProfile = profile.strategyProfile;
    }
    if (bot.opening) {
      bot.opening.race = race;
      if (profile.opening) {
        bot.opening.id = profile.opening.id;
        bot.opening.name = profile.opening.name;
        bot.opening.steps = profile.opening.steps;
      }
    }
  }
  function syncFields(bot) {
    $("bot-name").value = bot.name || "";
    $("bot-id").value = bot.id || "";
    $("strategy-profile").value = bot.strategyProfile || "";
    $("bot-race").value = bot.race || "Terran";
    $("command-race").value = bot.race || "Terran";
    $("default-role").value = bot.defaultGoals?.defaultRole || "engage";
    $("ai-enabled").checked = !!bot.ai?.enabled;
    renderCommandPreview();
  }
  function syncJsonFromFields() {
    const parsed = parse();
    if (parsed.error || !parsed.value) return;
    const bot = parsed.value;
    bot.name = $("bot-name").value;
    bot.id = $("bot-id").value;
    applyRace(bot, $("bot-race").value);
    $("strategy-profile").value = bot.strategyProfile;
    if (bot.defaultGoals) bot.defaultGoals.defaultRole = $("default-role").value;
    if (bot.ai) {
      bot.ai.enabled = $("ai-enabled").checked;
      if (!bot.ai.enabled) { bot.ai.maxOnStartCalls = 0; bot.ai.maxInMatchCalls = 0; }
      else {
        bot.ai.provider ||= "configured-by-host";
        bot.ai.model ||= "configured-by-host";
        if (bot.ai.maxOnStartCalls === 0 && bot.ai.maxInMatchCalls === 0) bot.ai.maxOnStartCalls = 1;
      }
    }
    editor.value = JSON.stringify(bot, null, 2);
    markDirty();
    renderCommandPreview();
  }
  function markDirty() {
    state.dirty = true;
    $("json-state").textContent = "Modified in this browser";
    $("validation-count").textContent = "Needs validation";
  }
  function validate() {
    const parsed = parse();
    const results = $("validation-results");
    if (parsed.error) {
      $("validation-count").textContent = "Invalid JSON";
      results.innerHTML = '<div class="error">JSON parse error: ' + escapeHtml(parsed.error) + "</div>";
      return false;
    }
    const errors = validateDefinition(parsed.value);
    if (errors.length) {
      $("validation-count").textContent = errors.length + " issue(s) found";
      results.innerHTML = errors.map(error => '<div class="error">× ' + escapeHtml(error) + "</div>").join("");
      return false;
    }
    state.lastValid = parsed.value;
    state.dirty = false;
    $("json-state").textContent = "Valid JSON · ready to export";
    $("validation-count").textContent = "Core contract checks passed";
    results.innerHTML = '<div class="ok">✓ JSON parses and the browser’s core BotDefinition v1 checks passed.</div><div class="fine-print">This is not the runtime’s full semantic validation. Import the artifact into SingularityBot for authoritative capability and match validation.</div>';
    return true;
  }
  async function loadSample() {
    try {
      const response = await fetch(sampleUrl, { cache: "no-store" });
      if (!response.ok) throw new Error("HTTP " + response.status);
      editor.value = JSON.stringify(await response.json(), null, 2);
      const parsed = parse();
      applyRace(parsed.value, parsed.value.race || "Terran");
      editor.value = JSON.stringify(parsed.value, null, 2);
      syncFields(parsed.value);
      markDirty();
      validate();
    } catch (error) {
      $("validation-results").innerHTML = '<div class="error">Could not load the checked-in example: ' + escapeHtml(error.message) + "</div>";
    }
  }
  function download() {
    if (!validate()) return;
    const blob = new Blob([JSON.stringify(state.lastValid, null, 2) + "\n"], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = (state.lastValid.id || "singularitybot").replace(/[^a-z0-9._-]+/gi, "-") + ".json";
    document.body.appendChild(link); link.click(); link.remove(); URL.revokeObjectURL(url);
    $("json-state").textContent = "Exported · " + link.download;
  }


  const commandProfiles = {
    Zerg: { strategy: "The Overmind", caption: "A distributed intelligence directing the swarm through its Cerebrates", coordinators: ["Mining Brood", "Spawning Brood", "Evolution Brood", "Defense Brood", "Swarm Command"] },
    Terran: { strategy: "Fleet Admiral", caption: "Strategic command delegated to specialist operational commanders", coordinators: ["Mining Operations", "Construction Corps", "Production Command", "Reconnaissance", "Fleet Command"] },
    Protoss: { strategy: "The Enclave", caption: "A strategic council expressing doctrine through appointed authorities", coordinators: ["Resource Stewardship", "Construction & Warp-In", "Production", "Judgment & Defense", "Reconnaissance"] }
  };
  function renderCommandPreview() {
    const race = $("command-race").value;
    const profile = commandProfiles[race];
    const custom = $("command-coordinators").value.split(",").map(x => x.trim()).filter(Boolean);
    $("command-strategy").value = profile.strategy;
    $("strategy-caption").textContent = profile.caption;
    $("command-preview").innerHTML = custom.map((name, index) =>
      '<div class="coordinator-card"><span>COORDINATOR ' + String(index + 1).padStart(2, "0") + '</span><strong>' + escapeHtml(name) + '</strong></div>'
    ).join("") + '<div class="controller-foot">↓ Each coordinator delegates assignments to persistent IController unit contexts and their FSM_API state machines.</div>';
  }
  $("command-race").addEventListener("change", () => {
    const race = $("command-race").value;
    const profile = commandProfiles[race];
    $("command-coordinators").value = profile.coordinators.join(", ");
    $("bot-race").value = race;
    syncJsonFromFields();
  });
  $("command-coordinators").addEventListener("input", renderCommandPreview);
  renderCommandPreview();

  const mapPreview = { map: null, frames: [], index: 0 };
  const svgNamespace = "http://www.w3.org/2000/svg";
  function svgElement(name, attributes, parent) {
    const node = document.createElementNS(svgNamespace, name);
    for (const [key, value] of Object.entries(attributes || {})) node.setAttribute(key, String(value));
    parent.appendChild(node);
    return node;
  }
  function mapTransform() {
    const bounds = mapPreview.map.fullBounds;
    const left = 42, top = 34, availableWidth = 916, availableHeight = 632;
    const scale = Math.min(availableWidth / (bounds.maxX - bounds.minX), availableHeight / (bounds.maxY - bounds.minY));
    const width = (bounds.maxX - bounds.minX) * scale;
    const height = (bounds.maxY - bounds.minY) * scale;
    const offsetX = left + (availableWidth - width) / 2;
    const offsetY = top + (availableHeight - height) / 2;
    return {
      x: value => offsetX + (value - bounds.minX) * scale,
      y: value => offsetY + height - (value - bounds.minY) * scale,
      rect: rect => ({ x: offsetX + (rect.minX - bounds.minX) * scale, y: offsetY + height - (rect.maxY - bounds.minY) * scale, width: (rect.maxX - rect.minX) * scale, height: (rect.maxY - rect.minY) * scale })
    };
  }
  function renderStaticMap() {
    const layer = $("map-static-layer");
    layer.replaceChildren();
    const map = mapPreview.map;
    const transform = mapTransform();
    const full = transform.rect(map.fullBounds);
    const playable = transform.rect(map.playableBounds);
    svgElement("rect", { ...full, rx: 8, class: "full-bounds" }, layer);
    svgElement("rect", { ...playable, rx: 3, class: "playable-bounds" }, layer);
    for (const start of map.startLocations) {
      const x = transform.x(start.x), y = transform.y(start.y);
      svgElement("circle", { cx: x, cy: y, r: 10, class: "start-point" }, layer);
      const label = svgElement("text", { x: x + 14, y: y - 12, class: "unit-label" }, layer);
      label.textContent = start.id;
    }
  }
  function renderMatchFrame() {
    const frame = mapPreview.frames[mapPreview.index];
    const layer = $("map-unit-layer");
    layer.replaceChildren();
    const transform = mapTransform();
    for (const unit of frame.units) {
      if (unit.contactState === "unknown" || !Number.isFinite(unit.x) || !Number.isFinite(unit.y)) continue;
      const x = transform.x(unit.x), y = transform.y(unit.y);
      const remembered = unit.contactState === "remembered";
      const unitClass = remembered ? "unit-remembered" : unit.alliance === "self" ? "unit-self" : unit.alliance === "enemy" ? "unit-enemy" : "unit-self";
      svgElement("circle", { cx: x, cy: y, r: remembered ? 9 : 7, class: unitClass }, layer);
      const label = svgElement("text", { x: x + 12, y: y - 9, class: "unit-label" }, layer);
      label.textContent = String(unit.tag);
    }
    $("frame-label").textContent = "Frame " + (mapPreview.index + 1) + " / " + mapPreview.frames.length + " · Loop " + frame.gameLoop + " · Observer P" + frame.observerPlayerId;
    $("destroyed-label").textContent = frame.authoritativelyDestroyedUnitTags.length
      ? "Destroyed (authoritative): " + frame.authoritativelyDestroyedUnitTags.join(", ")
      : "No destruction evidence";
    $("frame-scrubber").value = String(mapPreview.index);
    $("frame-previous").disabled = mapPreview.index === 0;
    $("frame-next").disabled = mapPreview.index === mapPreview.frames.length - 1;
  }
  async function loadMapPreview() {
    const status = $("map-status");
    try {
      const [mapResponse, firstResponse, secondResponse] = await Promise.all([
        fetch("../examples/map-documents/translated-two-start.map.v1.json", { cache: "no-store" }),
        fetch("../examples/match-snapshots/translated-two-start.frame-0001.snapshot.v1.json", { cache: "no-store" }),
        fetch("../examples/match-snapshots/translated-two-start.frame-0002.snapshot.v1.json", { cache: "no-store" })
      ]);
      for (const response of [mapResponse, firstResponse, secondResponse]) {
        if (!response.ok) throw new Error("Fixture request failed with HTTP " + response.status);
      }
      const map = await mapResponse.json();
      const frames = await Promise.all([firstResponse.json(), secondResponse.json()]);
      if (!map.fullBounds || !map.playableBounds || !Array.isArray(map.startLocations) || !Array.isArray(map.layers)) {
        throw new Error("MapDocument v1 is missing required geometry or layer metadata.");
      }
      if (frames.some(frame => frame.schemaVersion !== 1 || frame.mapId !== map.mapId || frame.mapContentHash !== map.contentHash)) {
        throw new Error("A match frame does not reference this exact map document.");
      }
      if (frames.some(frame => !Array.isArray(frame.capturedFields) ||
          !Array.isArray(frame.players) ||
          !frame.players.some(player => player.playerId === frame.observerPlayerId))) {
        throw new Error("A match frame must declare captured fields and include its observer in the player roster.");
      }
      frames.sort((a, b) => a.sequence - b.sequence);
      if (frames.some((frame, index) => index > 0 && frame.sequence <= frames[index - 1].sequence)) {
        throw new Error("Match frame sequence must be strictly increasing.");
      }
      mapPreview.map = map;
      mapPreview.frames = frames;
      mapPreview.index = 0;
      $("map-title").textContent = map.mapName;
      $("frame-scrubber").min = "0";
      $("frame-scrubber").max = String(frames.length - 1);
      $("frame-scrubber").disabled = frames.length < 2;
      status.textContent = map.layers.filter(layer => layer.availability === "available").length + " available layer(s)";
      renderStaticMap();
      renderMatchFrame();
    } catch (error) {
      status.textContent = "Preview unavailable";
      $("map-title").textContent = "Could not load map fixture";
      $("frame-label").textContent = error.message;
      $("map-static-layer").replaceChildren();
      $("map-unit-layer").replaceChildren();
    }
  }
  $("frame-scrubber").addEventListener("input", event => {
    mapPreview.index = Number(event.target.value);
    renderMatchFrame();
  });
  $("frame-previous").addEventListener("click", () => {
    if (mapPreview.index > 0) { mapPreview.index--; renderMatchFrame(); }
  });
  $("frame-next").addEventListener("click", () => {
    if (mapPreview.index < mapPreview.frames.length - 1) { mapPreview.index++; renderMatchFrame(); }
  });
  loadMapPreview();

  $("validate").addEventListener("click", validate);
  $("export").addEventListener("click", download);
  $("format-json").addEventListener("click", () => { const parsed = parse(); if (!parsed.error) editor.value = JSON.stringify(parsed.value, null, 2); else validate(); });
  $("reset-sample").addEventListener("click", loadSample);
  editor.addEventListener("input", markDirty);
  ["bot-name","bot-id","strategy-profile","default-role","ai-enabled","bot-race"].forEach(id => $(id).addEventListener("change", syncJsonFromFields));
  $("import-file").addEventListener("change", async event => {
    const file = event.target.files?.[0];
    if (!file) return;
    try {
      editor.value = await file.text();
      const parsed = parse();
      if (parsed.error) { validate(); return; }
      syncFields(parsed.value);
      markDirty();
      validate();
    } catch (error) {
      $("validation-results").innerHTML = '<div class="error">Import failed: ' + escapeHtml(error.message) + "</div>";
    } finally { event.target.value = ""; }
  });
  loadSample();
})();