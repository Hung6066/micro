package(default_visibility = ["//visibility:public"])

# Proto files shared across services
proto_library(
    name = "patient_proto",
    srcs = ["//src/Shared/Protos:patient.proto"],
    deps = ["@com_google_protobuf//:timestamp_proto"],
)

proto_library(
    name = "appointment_proto",
    srcs = ["//src/Shared/Protos:appointment.proto"],
    deps = ["@com_google_protobuf//:timestamp_proto"],
)

load("@rules_proto_grpc//csharp:defs.bzl", "csharp_grpc_library")

csharp_grpc_library(
    name = "patient_grpc",
    protos = [":patient_proto"],
)

csharp_grpc_library(
    name = "appointment_grpc",
    protos = [":appointment_proto"],
)

load("@io_bazel_rules_dotnet//dotnet:defs.bzl", "dotnet_library")

dotnet_library(
    name = "His.Hope.SharedKernel",
    srcs = glob(["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/**/*.cs"]),
)

dotnet_library(
    name = "His.Hope.EventBus",
    srcs = glob(["src/Shared/EventBus/Src/His.Hope.EventBus/**/*.cs"]),
)

dotnet_library(
    name = "His.Hope.EventBusRabbitMQ",
    srcs = glob(["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/**/*.cs"]),
    deps = [
        ":His.Hope.EventBus",
        "@RabbitMQ.Client//:net8.0",
        "@Polly//:net8.0",
    ],
)

dotnet_library(
    name = "His.Hope.IntegrationEvents",
    srcs = glob(["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/**/*.cs"]),
    deps = [":His.Hope.EventBus"],
)

dotnet_library(
    name = "His.Hope.Infrastructure",
    srcs = glob(["src/Shared/Infrastructure/His.Hope.Infrastructure/**/*.cs"]),
    deps = [
        ":His.Hope.EventBus",
        "@StackExchange.Redis//:net8.0",
        "@Consul//:net8.0",
        "@Polly//:net8.0",
        "@OpenTelemetry//:net8.0",
        "@OpenTelemetry.Exporter.Jaeger//:net8.0",
        "@OpenTelemetry.Instrumentation.AspNetCore//:net8.0",
    ],
)
