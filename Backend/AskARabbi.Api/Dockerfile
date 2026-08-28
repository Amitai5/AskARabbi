FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["Library/AskARabbiLIB/AskARabbiLIB.csproj", "Library/AskARabbiLIB/"]
COPY ["Backend/AskARabbi.Api/AskARabbi.Api.csproj", "Backend/AskARabbi.Api/"]
RUN dotnet restore "Backend/AskARabbi.Api/AskARabbi.Api.csproj"

COPY ["Library/AskARabbiLIB/", "Library/AskARabbiLIB/"]
COPY ["Backend/AskARabbi.Api/", "Backend/AskARabbi.Api/"]
RUN dotnet publish "Backend/AskARabbi.Api/AskARabbi.Api.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "AskARabbi.Api.dll"]
