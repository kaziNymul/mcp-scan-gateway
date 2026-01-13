# MCP Jurisdiction + Registry Plain Kubernetes Manifests
# For use with Mirantis Kubernetes Engine (MKE) or any Kubernetes cluster
#
# IMPORTANT: These manifests are provided for environments where Helm is not available.
# For production deployments, the Helm chart under /deploy/helm/mcp-jurisdiction is recommended.
#
# Prerequisites:
# 1. PostgreSQL database accessible from the cluster
# 2. (Optional) Redis for session caching
# 3. Container registry access for gateway and scanner images
#
# Deployment Order:
# 1. namespace.yaml - Create the namespace
# 2. secrets.yaml - Create secrets (EDIT with your actual values first!)
# 3. configmap.yaml - Create configuration
# 4. rbac.yaml - Create service accounts and RBAC
# 5. deployment.yaml - Deploy the gateway
# 6. service.yaml - Expose the gateway
# 7. ingress.yaml - (Optional) External access
# 8. networkpolicy.yaml - (Optional) Network segmentation
#
# Quick Start:
#   kubectl apply -f namespace.yaml
#   # Edit secrets.yaml with your PostgreSQL connection string!
#   kubectl apply -f secrets.yaml
#   kubectl apply -f configmap.yaml
#   kubectl apply -f rbac.yaml
#   kubectl apply -f deployment.yaml
#   kubectl apply -f service.yaml
#
# WARNING: Do NOT claim this solution can "scan laptops and detect all MCP servers."
# This is a GOVERNANCE BOUNDARY that:
# - Requires servers to be REGISTERED before they can be accessed
# - Scans registered servers via MCP-Scan Kubernetes Jobs
# - Enforces policy on gateway-proxied traffic only
