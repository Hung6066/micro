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
