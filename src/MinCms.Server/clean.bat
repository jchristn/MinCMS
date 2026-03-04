@echo off
echo Cleaning MinCMS runtime files...

if exist mincms.json (
    del mincms.json
    echo Deleted mincms.json
)

if exist logs (
    rmdir /s /q logs
    echo Deleted logs directory
)

echo Clean complete.
