# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY HCC2RestClient.csproj ./
RUN dotnet restore HCC2RestClient.csproj

# Copy the rest of the application code
COPY . ./

# Build the application
RUN dotnet build HCC2RestClient.csproj -c Release -o /app/build

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish HCC2RestClient.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Create the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the webhook server port
EXPOSE 8100

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "HCC2RestClient.dll"]