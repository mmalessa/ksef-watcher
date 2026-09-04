# Builds/tests run inside the .NET SDK Docker image — no local .NET SDK required.
# Pin the exact image tag; bump deliberately (see docs/09_integration_contracts.md's
# pinning rationale for the same reasoning applied to a NuGet dependency).
DOTNET_SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:8.0
SOLUTION         := KsefWatcher.sln
CONFIGURATION    ?= Release
NUGET_CACHE_VOL  := ksef-watcher-nuget-cache

DOCKER_RUN := docker run --rm \
	-v $(CURDIR):/src \
	-v $(NUGET_CACHE_VOL):/root/.nuget/packages \
	-w /src \
	$(DOTNET_SDK_IMAGE)

.PHONY: restore build test clean format shell

restore:
	$(DOCKER_RUN) dotnet restore $(SOLUTION)

build: restore
	$(DOCKER_RUN) dotnet build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

test: restore
	$(DOCKER_RUN) dotnet test $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

format:
	$(DOCKER_RUN) dotnet format $(SOLUTION)

clean:
	$(DOCKER_RUN) dotnet clean $(SOLUTION) || true
	find . -type d \( -name bin -o -name obj \) -not -path './.git/*' -prune -exec rm -rf {} +

# Interactive SDK shell for ad-hoc commands (dotnet add package, dotnet new, …).
shell:
	docker run --rm -it \
		-v $(CURDIR):/src \
		-v $(NUGET_CACHE_VOL):/root/.nuget/packages \
		-w /src \
		$(DOTNET_SDK_IMAGE) bash
