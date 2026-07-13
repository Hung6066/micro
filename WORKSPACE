workspace(name = "his_hope")

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

# .NET SDK
http_archive(
    name = "io_bazel_rules_dotnet",
    sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
    strip_prefix = "rules_dotnet-0.14.0",
    urls = ["https://github.com/bazelbuild/rules_dotnet/releases/download/v0.14.0/rules_dotnet-v0.14.0.tar.gz"],
)

load(
    "@io_bazel_rules_dotnet//dotnet:deps.bzl",
    "dotnet_register_toolchains",
    "dotnet_repositories_nuget",
)

dotnet_register_toolchains(
    dotnet_version = "8.0",
)

dotnet_repositories_nuget()

load("//bazel:nuget.bzl", "nuget_packages")

nuget_packages()

# Protocol Buffers rules
http_archive(
    name = "rules_proto",
    sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
    strip_prefix = "rules_proto-6.0.2",
    urls = ["https://github.com/bazelbuild/rules_proto/releases/download/6.0.2/rules_proto-6.0.2.tar.gz"],
)

load("@rules_proto//proto:repositories.bzl", "rules_proto_dependencies", "rules_proto_toolchains")
rules_proto_dependencies()
rules_proto_toolchains()

# gRPC rules
http_archive(
    name = "rules_proto_grpc",
    sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
    strip_prefix = "rules_proto_grpc-4.6.0",
    urls = ["https://github.com/rules-proto-grpc/rules_proto_grpc/releases/download/4.6.0/rules_proto_grpc-4.6.0.tar.gz"],
)

load("@rules_proto_grpc//:repositories.bzl", "rules_proto_grpc_repos", "rules_proto_grpc_toolchains")
rules_proto_grpc_repos()
rules_proto_grpc_toolchains()

# Docker rules
http_archive(
    name = "io_bazel_rules_docker",
    sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
    strip_prefix = "rules_docker-0.25.0",
    urls = ["https://github.com/bazelbuild/rules_docker/releases/download/v0.25.0/rules_docker-v0.25.0.tar.gz"],
)

load(
    "@io_bazel_rules_docker//repositories:repositories.bzl",
    container_repositories,
)
container_repositories()

load(
    "@io_bazel_rules_docker//dotnet:dotnet.bzl",
    "dotnet_repositories",
)
dotnet_repositories()

load("@io_bazel_rules_docker//repositories:deps.bzl", container_deps = "deps")
container_deps()
