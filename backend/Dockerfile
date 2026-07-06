FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EducationCrm.Api.csproj ./
RUN dotnet restore EducationCrm.Api.csproj

COPY . ./
RUN dotnet publish EducationCrm.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish ./

EXPOSE 8080
CMD ["sh", "-c", "dotnet EducationCrm.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
