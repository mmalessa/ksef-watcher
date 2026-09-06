# Builds/tests run inside the .NET SDK Docker image — no local .NET SDK required.
# Pin the exact image tag; bump deliberately (see docs/09_integration_contracts.md's
# pinning rationale for the same reasoning applied to a NuGet dependency).
DOTNET_SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:8.0
SOLUTION         := KsefWatcher.sln
CONFIGURATION    ?= Release
NUGET_CACHE_VOL  := ksef-watcher-nuget-cache

# Vendored KSeF client (vendor/README.md) — not committed, not on nuget.org. Pin bumped
# deliberately (docs/09_integration_contracts.md); update both here and vendor/README.md together.
VENDOR_DIR    := vendor/ksef-client-csharp
VENDOR_REPO   := https://github.com/CIRFMF/ksef-client-csharp.git
VENDOR_COMMIT := 04f01c1c7834336a3aef1804149cd5bcbd883a3e

DOCKER_RUN := docker run --rm \
	-v $(CURDIR):/src \
	-v $(NUGET_CACHE_VOL):/root/.nuget/packages \
	-w /src \
	$(DOTNET_SDK_IMAGE)

PUBLISH_RID ?= linux-x64

.PHONY: init restore build test publish clean format shell help
help:
	@grep -E '(^[a-zA-Z0-9_-]+:.*?##.*$$)|(^##)' Makefile | awk 'BEGIN {FS = ":.*?## "}{printf "\033[32m%-30s\033[0m %s\n", $$1, $$2}' | sed -e 's/\[32m##/[33m/'


# Fetches the vendored KSeF client at its pinned commit and applies the required net8.0-only
# patch (our SDK image can't even restore a net9.0/net10.0-multi-targeted project). Idempotent —
# safe to re-run; skips the clone if $(VENDOR_DIR) already exists.
init: ## init source code
	@if [ -d $(VENDOR_DIR) ]; then \
		echo "$(VENDOR_DIR) already exists — skipping clone (remove it first to re-fetch)."; \
	else \
		git clone $(VENDOR_REPO) $(VENDOR_DIR) && \
		git -C $(VENDOR_DIR) checkout $(VENDOR_COMMIT); \
	fi
	sed -i 's#<TargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</TargetFrameworks>#<TargetFramework>net8.0</TargetFramework>#' \
		$(VENDOR_DIR)/KSeF.Client/KSeF.Client.csproj \
		$(VENDOR_DIR)/KSeF.Client.ClientFactory/KSeF.Client.ClientFactory.csproj

restore: ## dotnet restore
	$(DOCKER_RUN) dotnet restore $(SOLUTION)

build: restore ## build 
	$(DOCKER_RUN) dotnet build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

test: restore ## test
	$(DOCKER_RUN) dotnet test $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

# Single self-contained binary at ./bin/ksef-watcher (no .NET runtime needed on the target
# machine — matches A6's "runs unattended as a systemd service" with a one-file deploy artifact).
# PUBLISH_RID picks the target machine's architecture (e.g. `make publish PUBLISH_RID=linux-arm64`).
# Publishes straight into ./bin without wiping it first — DebugType=embedded and
# GenerateDocumentationFile=false below mean dotnet publish only ever writes the one binary file,
# so anything else you keep there (e.g. your own config.yaml) survives repeated publishes.
publish: ## create self-contained binary
	$(DOCKER_RUN) dotnet publish src/KsefWatcher.Host/KsefWatcher.Host.csproj \
		--configuration $(CONFIGURATION) \
		--runtime $(PUBLISH_RID) \
		--self-contained true \
		-p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:DebugType=embedded \
		-p:GenerateDocumentationFile=false \
		-o bin

format: ## dotnet format
	$(DOCKER_RUN) dotnet format $(SOLUTION)

clean: ## dotnet clean
	$(DOCKER_RUN) dotnet clean $(SOLUTION) || true
	find . -type d \( -name bin -o -name obj \) -not -path './.git/*' -prune -exec rm -rf {} +

# Interactive SDK shell for ad-hoc commands (dotnet add package, dotnet new, …).
shell: ## interactive shell inside container
	docker run --rm -it \
		-v $(CURDIR):/src \
		-v $(NUGET_CACHE_VOL):/root/.nuget/packages \
		-w /src \
		$(DOTNET_SDK_IMAGE) bash
