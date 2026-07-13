load("@io_bazel_rules_dotnet//dotnet:defs.bzl", "dotnet_library", "dotnet_binary", "dotnet_test")
load("@io_bazel_rules_docker//dotnet:image.bzl", "dotnet_image")

dotnet_library(
    name = "IdentityService.Domain",
    srcs = glob(["src/Services/IdentityService/IdentityService.Domain/**/*.cs"]),
    deps = [
        "//bazel:His.Hope.SharedKernel",
        "@EntityFrameworkCore//:net8.0",
    ],
)

dotnet_library(
    name = "IdentityService.Application",
    srcs = glob(["src/Services/IdentityService/IdentityService.Application/**/*.cs"]),
    deps = [
        ":IdentityService.Domain",
        "@MediatR//:net8.0",
        "@FluentValidation//:net8.0",
    ],
)

dotnet_library(
    name = "IdentityService.Infrastructure",
    srcs = glob(["src/Services/IdentityService/IdentityService.Infrastructure/**/*.cs"]),
    deps = [
        ":IdentityService.Domain",
        ":IdentityService.Application",
        "@EntityFrameworkCore//:net8.0",
        "@EntityFrameworkCore.Npgsql//:net8.0",
    ],
)

dotnet_binary(
    name = "IdentityService.Api",
    srcs = glob(["src/Services/IdentityService/IdentityService.Api/**/*.cs"]),
    deps = [
        ":IdentityService.Application",
        ":IdentityService.Infrastructure",
        "//bazel:His.Hope.Infrastructure",
        "@Serilog//:net8.0",
        "@Serilog.Sinks.ElasticSearch//:net8.0",
    ],
)

dotnet_test(
    name = "IdentityService.Domain.Tests",
    srcs = glob(["tests/IdentityService.Domain.Tests/**/*.cs"]),
    deps = [
        ":IdentityService.Domain",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_test(
    name = "IdentityService.Application.Tests",
    srcs = glob(["tests/IdentityService.Application.Tests/**/*.cs"]),
    deps = [
        ":IdentityService.Application",
        "@xunit//:net8.0",
        "@Moq//:net8.0",
        "@FluentAssertions//:net8.0",
    ],
)

dotnet_image(
    name = "identity_service_image",
    binary = ":IdentityService.Api",
    tag = "{BUILD_TIMESTAMP}",
)
