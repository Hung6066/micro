load("@io_bazel_rules_dotnet//dotnet:defs.bzl", "dotnet_library", "dotnet_binary", "dotnet_test")
load("@io_bazel_rules_docker//dotnet:image.bzl", "dotnet_image")

dotnet_library(
    name = "PatientService.Domain",
    srcs = glob(["src/Services/PatientService/PatientService.Domain/**/*.cs"]),
    deps = [
        "//bazel:His.Hope.SharedKernel",
    ],
)

dotnet_library(
    name = "PatientService.Application",
    srcs = glob(["src/Services/PatientService/PatientService.Application/**/*.cs"]),
    deps = [
        ":PatientService.Domain",
        "//bazel:His.Hope.SharedKernel",
        "@MediatR//:net8.0",
        "@AutoMapper//:net8.0",
        "@FluentValidation//:net8.0",
    ],
)

dotnet_library(
    name = "PatientService.Infrastructure",
    srcs = glob(["src/Services/PatientService/PatientService.Infrastructure/**/*.cs"]),
    deps = [
        ":PatientService.Domain",
        ":PatientService.Application",
        "//bazel:His.Hope.SharedKernel",
        "@EntityFrameworkCore//:net8.0",
        "@EntityFrameworkCore.Npgsql//:net8.0",
    ],
)

dotnet_binary(
    name = "PatientService.Api",
    srcs = glob(["src/Services/PatientService/PatientService.Api/**/*.cs"]),
    deps = [
        ":PatientService.Application",
        ":PatientService.Infrastructure",
        "//bazel:His.Hope.Infrastructure",
        "//bazel:His.Hope.EventBusRabbitMQ",
        "//bazel:His.Hope.IntegrationEvents",
        "//bazel:patient_grpc",
        "@Grpc.AspNetCore//:net8.0",
        "@Serilog//:net8.0",
        "@Serilog.Sinks.ElasticSearch//:net8.0",
    ],
)

dotnet_test(
    name = "PatientService.Domain.Tests",
    srcs = glob(["tests/PatientService.Domain.Tests/**/*.cs"]),
    deps = [
        ":PatientService.Domain",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_test(
    name = "PatientService.Application.Tests",
    srcs = glob(["tests/PatientService.Application.Tests/**/*.cs"]),
    deps = [
        ":PatientService.Application",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_image(
    name = "patient_service_image",
    binary = ":PatientService.Api",
    tag = "{BUILD_TIMESTAMP}",
)
