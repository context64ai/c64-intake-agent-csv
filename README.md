# 🚀 CSV Intake Agent


[![Docs](https://img.shields.io/badge/docs-Data%20Context%20Hub-blue?logo=read-the-docs&style=flat)](https://docs.datacontexthub.com/)
![Status](https://img.shields.io/badge/status-stable-brightgreen)
[![License: GNU](https://img.shields.io/badge/license-GNU-green?style=flat)](LICENSE)

---

This Repository contains a focused, production-ready agent for ingesting CSV data into a [Data Context Hub (DCH)](https://docs.datacontexthub.com/) knowledge graph.  
This repository contains code, configuration templates, and examples to extract CSV data, apply schema/mapping rules, and hand data off to DCH ingestion components.

---
## 📋 Requirements

**Before you start, make sure you meet the following requirements:**

- 🧠 Access to a DCH instance
- 🐧 A *Linux machine* with sudo/root access
- 🌐 **Internet connectivity** for downloading dependencies  
- 🐙 Git installed and access to GitHub (for cloning this repository)

## 🐳 Build Docker Image and Run Container

To build the docker image execute
```docker
docker build -t c64-intake-agent-csv .
```

To run a container with the docker image execute
```docker
docker run --rm -it --name c64-intake-agent-csv --env-file <your-env-file.env> -p 8080:8080 c64-intake-agent-csv
```

## 🛠️ Support

Context64 provided scripts in this GitHub project. For direct issues, please refer to the Issues tab of this GitHub project.

For other questions related to this project, contact us at `github@c64.ai`

## 📄 License

This project is licensed under the GNU GENERAL PUBLIC LICENSE – see the [LICENSE](LICENSE) file for details.

## 🎯 External Software 

![NuGet](https://img.shields.io/badge/NuGet-004880?style=flat&logo=nuget&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)