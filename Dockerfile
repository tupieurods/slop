FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/SlopChat/SlopChat.csproj src/SlopChat/
RUN dotnet restore src/SlopChat/SlopChat.csproj

COPY src/SlopChat/ src/SlopChat/
RUN dotnet publish src/SlopChat/SlopChat.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SlopChat.dll"]
