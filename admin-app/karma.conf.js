module.exports = function (config) {
  config.set({
    basePath: "",
    frameworks: ["jasmine", "@angular-devkit/build-angular"],
    plugins: [
      require("karma-jasmine"),
      require("karma-chrome-launcher"),
      require("karma-jasmine-html-reporter"),
      require("karma-coverage"),
      require("@angular-devkit/build-angular/plugins/karma"),
    ],
    reporters: ["progress", "kjhtml"],
    coverageReporter: {
      dir: "coverage/admin-app",
      reporters: [{ type: "text-summary" }, { type: "html" }],
      check: {
        global: {
          statements: 39,
          branches: 43,
          functions: 20,
          lines: 39,
        },
      },
    },
  });
};
