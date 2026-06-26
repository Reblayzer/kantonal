FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Kantonal.sln ./
COPY src/Kantonal.Domain/Kantonal.Domain.csproj src/Kantonal.Domain/
COPY src/Kantonal.Application/Kantonal.Application.csproj src/Kantonal.Application/
COPY src/Kantonal.Infrastructure/Kantonal.Infrastructure.csproj src/Kantonal.Infrastructure/
COPY src/Kantonal.Api/Kantonal.Api.csproj src/Kantonal.Api/
RUN dotnet restore src/Kantonal.Api/Kantonal.Api.csproj
COPY . .
RUN dotnet publish src/Kantonal.Api/Kantonal.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kantonal.Api.dll"]
