FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY src/SlopChat/SlopChat.csproj src/SlopChat/
RUN dotnet restore src/SlopChat/SlopChat.csproj

COPY src/SlopChat/ src/SlopChat/
RUN dotnet publish src/SlopChat/SlopChat.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SlopChat.dll"]
