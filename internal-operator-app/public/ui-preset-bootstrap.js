(function () {
  var allowed = ["expo", "linear", "intercom"];
  var requested =
    new URLSearchParams(location.search).get("ui") ||
    localStorage.getItem("hh-ui-preset") ||
    "expo";
  document.documentElement.dataset.uiPreset = allowed.indexOf(requested) >= 0
    ? requested
    : "expo";
})();

(function () {
  var stored = localStorage.getItem("hh-theme");
  var mode = stored === "light" || stored === "dark" || stored === "system" ? stored : "system";
  var resolved = mode === "system"
    ? (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light")
    : mode;
  document.documentElement.dataset.theme = resolved;
  document.documentElement.dataset.themeMode = mode;
  document.documentElement.style.colorScheme = resolved;
})();
