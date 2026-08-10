load("@io_bazel_rules_dotnet//dotnet:defs.bzl", "dotnet_library", "dotnet_binary", "dotnet_test")
load("@io_bazel_rules_docker//dotnet:image.bzl", "dotnet_image")

dotnet_library(
    name = "AppointmentService.Domain",
    srcs = glob(["src/Services/AppointmentService/AppointmentService.Domain/**/*.cs"]),
    deps = [
        "//bazel:His.Hope.SharedKernel",
    ],
)

dotnet_binary(
    name = "AppointmentService.Api",
    srcs = glob(["src/Services/AppointmentService/AppointmentService.Api/**/*.cs"]),
    deps = [
        ":AppointmentService.Domain",
        "//bazel:His.Hope.Infrastructure",
        "//bazel:His.Hope.EventBusRabbitMQ",
        "//bazel:His.Hope.IntegrationEvents",
        "//bazel:patient_grpc",
        "//bazel:appointment_grpc",
        "@Grpc.AspNetCore//:net8.0",
        "@Serilog//:net8.0",
        "@Serilog.Sinks.ElasticSearch//:net8.0",
    ],
)

dotnet_test(
    name = "AppointmentService.Domain.Tests",
    srcs = glob(["tests/AppointmentService.Domain.Tests/**/*.cs"]),
    deps = [
        ":AppointmentService.Domain",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_image(
    name = "appointment_service_image",
    binary = ":AppointmentService.Api",
    tag = "{BUILD_TIMESTAMP}",
)
