FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Library/AskARabbiLIB/ Library/AskARabbiLIB/
COPY Tools/AskARabbi.DictionaryImporter/ Tools/AskARabbi.DictionaryImporter/
RUN dotnet publish Tools/AskARabbi.DictionaryImporter/AskARabbi.DictionaryImporter.csproj --configuration Release --output /app/publish /p:UseAppHost=false
RUN dotnet /app/publish/AskARabbi.DictionaryImporter.dll download /app/bdb

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/bdb /app/bdb
USER $APP_UID
ENTRYPOINT ["dotnet", "AskARabbi.DictionaryImporter.dll"]
