call dotnet nuget add source "https://nexus.office.sprut.ru/repository/dev-feed/index.json" -n "dev-feed"
call dotnet nuget add source "https://nexus.office.sprut.ru/repository/master-feed/index.json" -n "master-feed"
call dotnet nuget add source "https://nexus.office.sprut.ru/repository/nuget.org-proxy/index.json" -n "nuget_org_proxy"
call dotnet nuget disable source nuget.org

EXIT /B 0