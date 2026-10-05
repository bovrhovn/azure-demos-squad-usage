FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/SquadDemos.Web.CopilotDynamicModule.csproj", "src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/"]
RUN dotnet restore "src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/SquadDemos.Web.CopilotDynamicModule.csproj"
COPY ["src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/", "src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/"]
WORKDIR "/src/src/SquadDemos/SquadDemos.Web.CopilotDynamicModule"
RUN dotnet publish "SquadDemos.Web.CopilotDynamicModule.csproj" --configuration $BUILD_CONFIGURATION --output /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SquadDemos.Web.CopilotDynamicModule.dll"]
