# Pinned to exact patch tags rather than a floating "10.0" - a floating tag means every rebuild
# silently pulls whatever Microsoft ships next, with no diff to review and no way to tell after the
# fact which build used which base image. Bump these deliberately.
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

# Copy the project file first so restore is cached independently of source changes.
COPY SoftCo/SoftCo.csproj SoftCo/
RUN dotnet restore SoftCo/SoftCo.csproj

COPY SoftCo/ SoftCo/
RUN dotnet publish SoftCo/SoftCo.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user baked into the base image rather than root.
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SoftCo.dll"]
