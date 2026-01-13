{{/*
Expand the name of the chart.
*/}}
{{- define "mcp-jurisdiction.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "mcp-jurisdiction.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "mcp-jurisdiction.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "mcp-jurisdiction.labels" -}}
helm.sh/chart: {{ include "mcp-jurisdiction.chart" . }}
{{ include "mcp-jurisdiction.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "mcp-jurisdiction.selectorLabels" -}}
app.kubernetes.io/name: {{ include "mcp-jurisdiction.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "mcp-jurisdiction.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "mcp-jurisdiction.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
PostgreSQL connection string
*/}}
{{- define "mcp-jurisdiction.postgresConnection" -}}
{{- if .Values.postgresql.enabled }}
Host={{ include "mcp-jurisdiction.fullname" . }}-postgresql;Database={{ .Values.postgresql.auth.database }};Username={{ .Values.postgresql.auth.username }};Password={{ .Values.postgresql.auth.password }}
{{- else }}
Host={{ .Values.externalPostgres.host }};Port={{ .Values.externalPostgres.port }};Database={{ .Values.externalPostgres.database }};Username={{ .Values.externalPostgres.username }};Password={{ .Values.externalPostgres.password }}
{{- end }}
{{- end }}

{{/*
Redis connection string
*/}}
{{- define "mcp-jurisdiction.redisConnection" -}}
{{- if .Values.redis.enabled }}
{{ include "mcp-jurisdiction.fullname" . }}-redis-master:6379
{{- else }}
localhost:6379
{{- end }}
{{- end }}

{{/*
Scanner image
*/}}
{{- define "mcp-jurisdiction.scannerImage" -}}
{{- if .Values.global.imageRegistry }}
{{- printf "%s/%s:%s" .Values.global.imageRegistry .Values.scanner.image.repository .Values.scanner.image.tag }}
{{- else }}
{{- printf "%s:%s" .Values.scanner.image.repository .Values.scanner.image.tag }}
{{- end }}
{{- end }}
