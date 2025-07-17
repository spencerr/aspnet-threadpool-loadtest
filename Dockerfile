FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ThreadPoolDemo.csproj", "."]
RUN dotnet restore "ThreadPoolDemo.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "ThreadPoolDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ThreadPoolDemo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for better observability
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://*:8080
ENV Logging__LogLevel__Default=Information
ENV Logging__LogLevel__ThreadPoolDemo=Information

ENTRYPOINT ["dotnet", "ThreadPoolDemo.dll"]