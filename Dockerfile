FROM mcr.microsoft.com/dotnet/sdk:5.0
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

RUN ls

ENTRYPOINT ["dotnet", "out/CS301-batch.dll"]