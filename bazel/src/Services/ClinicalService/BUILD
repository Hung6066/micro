load("@io_bazel_rules_dotnet//dotnet:defs.bzl", "dotnet_library", "dotnet_binary", "dotnet_test")
load("@io_bazel_rules_docker//dotnet:image.bzl", "dotnet_image")

dotnet_library(
    name = "ClinicalService.Domain",
    srcs = glob(["src/Services/ClinicalService/ClinicalService.Domain/**/*.cs"]),
    deps = [
        "//bazel:His.Hope.SharedKernel",
    ],
)

dotnet_binary(
    name = "ClinicalService.Api",
    srcs = glob(["src/Services/ClinicalService/ClinicalService.Api/**/*.cs"]),
    deps = [
        ":ClinicalService.Domain",
        "//bazel:His.Hope.Infrastructure",
        "//bazel:His.Hope.EventBusRabbitMQ",
        "//bazel:His.Hope.IntegrationEvents",
        "//bazel:patient_grpc",
        "//bazel:appointment_grpc",
        "@Grpc.AspNetCore//:net8.0",
        "@EntityFrameworkCore.Npgsql//:net8.0",
        "@Serilog//:net8.0",
        "@Serilog.Sinks.ElasticSearch//:net8.0",
    ],
)

dotnet_test(
    name = "ClinicalService.Domain.Tests",
    srcs = glob(["tests/ClinicalService.Domain.Tests/**/*.cs"]),
    deps = [
        ":ClinicalService.Domain",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_image(
    name = "clinical_service_image",
    binary = ":ClinicalService.Api",
    tag = "{BUILD_TIMESTAMP}",
)
