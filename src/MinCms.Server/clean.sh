#!/bin/bash
echo "Cleaning MinCMS runtime files..."

if [ -f mincms.json ]; then
    rm mincms.json
    echo "Deleted mincms.json"
fi

if [ -d logs ]; then
    rm -rf logs
    echo "Deleted logs directory"
fi

echo "Clean complete."
