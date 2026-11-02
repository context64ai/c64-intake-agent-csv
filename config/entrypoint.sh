#!/usr/bin/env bash
update-ca-certificates > update-ca-certificates.log 2>&1 || true
echo "Starting application" 
dotnet DCH.CsvIntakeAgent.dll