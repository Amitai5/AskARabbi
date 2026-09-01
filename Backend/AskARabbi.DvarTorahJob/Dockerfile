FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["Library/AskARabbiLIB/AskARabbiLIB.csproj", "Library/AskARabbiLIB/"]
COPY ["Backend/AskARabbi.DvarTorahJob/AskARabbi.DvarTorahJob.csproj", "Backend/AskARabbi.DvarTorahJob/"]
RUN dotnet restore "Backend/AskARabbi.DvarTorahJob/AskARabbi.DvarTorahJob.csproj"

COPY ["Library/AskARabbiLIB/", "Library/AskARabbiLIB/"]
COPY ["Backend/AskARabbi.DvarTorahJob/", "Backend/AskARabbi.DvarTorahJob/"]
COPY ["Data/NormalizedData/Sefaria/Metadata/document-manifest.json", "Data/NormalizedData/Sefaria/Metadata/"]
RUN dotnet publish "Backend/AskARabbi.DvarTorahJob/AskARabbi.DvarTorahJob.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "AskARabbi.DvarTorahJob.dll"]
