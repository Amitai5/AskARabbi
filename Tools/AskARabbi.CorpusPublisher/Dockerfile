FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Library/AskARabbiLIB/ Library/AskARabbiLIB/
COPY Tools/AskARabbi.CorpusPublisher/ Tools/AskARabbi.CorpusPublisher/
RUN dotnet publish Tools/AskARabbi.CorpusPublisher/AskARabbi.CorpusPublisher.csproj --configuration Release --output /app/publish /p:UseAppHost=false
COPY Backend/AskARabbi.Api/Data/canonical-sources.zip Backend/AskARabbi.Api/Data/canonical-sources.zip
COPY Data/NormalizedData/Sefaria/Metadata/document-manifest.json Data/NormalizedData/Sefaria/Metadata/document-manifest.json
RUN dotnet /app/publish/AskARabbi.CorpusPublisher.dll index-bundle --index /app/publish/Data/canonical-search.sqlite

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY Backend/AskARabbi.Api/Data/canonical-sources.zip Backend/AskARabbi.Api/Data/canonical-sources.zip
COPY Data/NormalizedData/Sefaria/Metadata/document-manifest.json Data/NormalizedData/Sefaria/Metadata/document-manifest.json
COPY Prototype/Prompts/ Prototype/Prompts/
USER $APP_UID
ENTRYPOINT ["dotnet", "AskARabbi.CorpusPublisher.dll"]
