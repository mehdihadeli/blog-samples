{{- define "vllm-stack.name" -}}
{{- .Chart.Name -}}
{{- end -}}

{{- define "vllm-stack.fullname" -}}
{{- .Release.Name -}}
{{- end -}}

{{- define "vllm-stack.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" -}}
{{- end -}}

{{- define "vllm-stack.labels" -}}
helm.sh/chart: {{ include "vllm-stack.chart" . }}
app.kubernetes.io/name: {{ include "vllm-stack.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "vllm-stack.selectorLabels" -}}
app.kubernetes.io/name: {{ include "vllm-stack.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}