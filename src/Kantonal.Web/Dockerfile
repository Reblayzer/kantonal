FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Kantonal.sln ./
COPY src/Kantonal.Web/Kantonal.Web.csproj src/Kantonal.Web/
RUN dotnet restore src/Kantonal.Web/Kantonal.Web.csproj
COPY . .
RUN dotnet publish src/Kantonal.Web/Kantonal.Web.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kantonal.Web.dll"]
