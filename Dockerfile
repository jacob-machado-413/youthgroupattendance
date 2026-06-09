FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/YouthGroupAttendance.Api.csproj backend/
COPY frontend/YouthGroupAttendance.Frontend.csproj frontend/

RUN dotnet restore backend/YouthGroupAttendance.Api.csproj
RUN dotnet restore frontend/YouthGroupAttendance.Frontend.csproj

COPY . .

RUN dotnet publish frontend/YouthGroupAttendance.Frontend.csproj \
    -c Release -o /publish/frontend

RUN dotnet publish backend/YouthGroupAttendance.Api.csproj \
    -c Release -o /publish/backend

RUN mkdir -p /publish/backend/wwwroot && \
    cp -r /publish/frontend/wwwroot/* /publish/backend/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /publish/backend .

ENTRYPOINT ["dotnet", "YouthGroupAttendance.Api.dll"]
