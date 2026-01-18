ARG GITHUB_CLIENT_ID=your-github-client-id-here
ARG GITHUB_CLIENT_SECRET=your-github-client-secret-here
ARG CONNECTION_STRING="Data Source=Purrnet.db"
ARG ASPNETCORE_ENVIRONMENT=Development

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY . ./
RUN dotnet restore "purrnet.csproj"

# Publish
WORKDIR /src
RUN dotnet publish "purrnet.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
COPY --from=build /app/publish ./
# Expose build-time args as runtime environment variables (can be overridden at container run)
ENV GITHUB_CLIENT_ID=${GITHUB_CLIENT_ID}
ENV GITHUB_CLIENT_SECRET=${GITHUB_CLIENT_SECRET}
ENV CONNECTION_STRING=${CONNECTION_STRING}
ENV ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
EXPOSE 80
ENTRYPOINT ["dotnet", "purrnet.dll"]
