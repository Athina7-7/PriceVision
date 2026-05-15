import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EvmCalculationResponse {
  recordId: string;
  projectId: string;
  periodDateUtc: string;
  pv: number;
  ev: number;
  ac: number;
  cpi: number;
  spi: number;
  costInterpretation: string;
  scheduleInterpretation: string;
}

export interface EvmHistoryPoint {
  periodDateUtc: string;
  pv: number;
  ev: number;
  ac: number;
  cpi: number;
  spi: number;
  costInterpretation: string;
  scheduleInterpretation: string;
}

export interface CreateProjectRequest {
  name: string;
  areaM2: number;
  location: string;
  type: string;
  durationMonths: number;
  baseCostCop: number;
}

export interface ProjectValidationWarningResponse {
  code: string;
  title: string;
  message: string;
}

export interface CreatePredictionForProjectRequest {
  predictMaterials: boolean;
  predictLabor: boolean;
}

export interface ProjectPredictionResponse {
  predictionId: string;
  projectId: string;
  name: string;
  areaM2: number;
  location: string;
  type: string;
  durationMonths: number;
  baseCostCop: number;
  createdAtUtc: string;
  predictMaterials: boolean;
  predictLabor: boolean;
  modelType: string;
  modelVersion: string;
  materialesEstimados: {
    quantity: number;
    costCop: number;
  } | null;
  manoObraRequeridaHorasPersona: number | null;
}

export interface FinancialPredictionResponse {
  financialPredictionId: string;
  projectId: string;
  projectName: string;
  areaM2: number;
  type: string;
  location: string;
  durationMonths: number;
  baseCostCop: number;
  estimatedTotalCostCop: number;
  minimumEstimatedCostCop: number;
  maximumEstimatedCostCop: number;
  confidencePercentage: number;
  confidenceLevel: string;
  standardError: number;
  confidenceIntervalLower: number;
  confidenceIntervalUpper: number;
  confidenceExplanation: string;
  historicalAverageCostPerM2Cop: number;
  locationTrendFactor: number;
  modelType: string;
  modelVersion: string;
  createdAtUtc: string;
}

export interface ProjectSummaryResponse {
  projectId: string;
  name: string;
  areaM2: number;
  location: string;
  type: string;
  durationMonths: number;
  baseCostCop: number;
  createdAtUtc: string;
  hasPrediction: boolean;
  hasMaterialsPrediction: boolean;
  hasLaborPrediction: boolean;
  hasFinancialPrediction: boolean;
  hasEvm: boolean;
}

export interface CreateProjectResponse {
  project: ProjectSummaryResponse;
  validationWarnings: ProjectValidationWarningResponse[];
}

export interface ProjectActionHistoryItem {
  actionType: 'project' | 'prediction' | 'evm' | string;
  occurredAtUtc: string;
  title: string;
  summary: string;
}

export interface PredictionHistoryResponse {
  predictionId: string;
  projectId: string;
  projectName: string;
  areaM2: number;
  type: string;
  location: string;
  durationMonths: number;
  baseCostCop: number;
  predictedMaterials: boolean;
  predictedLabor: boolean;
  estimatedMaterialQuantity: number;
  estimatedMaterialCostCop: number;
  requiredLaborHours: number;
  modelType: string;
  modelVersion: string;
  createdAtUtc: string;
}

export interface EvmSummaryResponse {
  recordId: string;
  projectId: string;
  projectName: string;
  areaM2: number;
  type: string;
  location: string;
  durationMonths: number;
  baseCostCop: number;
  periodDateUtc: string;
  pv: number;
  ev: number;
  ac: number;
  cpi: number;
  spi: number;
  costInterpretation: string;
  scheduleInterpretation: string;
  createdAtUtc: string;
}

export interface FinancialPredictionSummaryResponse {
  financialPredictionId: string;
  projectId: string;
  projectName: string;
  areaM2: number;
  type: string;
  location: string;
  durationMonths: number;
  baseCostCop: number;
  estimatedTotalCostCop: number;
  minimumEstimatedCostCop: number;
  maximumEstimatedCostCop: number;
  confidencePercentage: number;
  confidenceLevel: string;
  standardError: number;
  confidenceIntervalLower: number;
  confidenceIntervalUpper: number;
  confidenceExplanation: string;
  historicalAverageCostPerM2Cop: number;
  locationTrendFactor: number;
  modelType: string;
  modelVersion: string;
  createdAtUtc: string;
}

export interface SimulationRequest {
  simulatedDurationMonths: number;
  simulatedBaseCostCop: number;
}

export interface SimulationMetricComparison {
  label: string;
  originalValue: number;
  simulatedValue: number;
  absoluteDifference: number;
  percentageDifference: number;
}

export interface SimulationResult {
  projectId: string;
  projectName: string;
  simulatedAtUtc: string;
  metrics: SimulationMetricComparison[];
  originalEstimatedTotalCostCop: number;
  simulatedEstimatedTotalCostCop: number;
  estimatedTotalCostDifferenceCop: number;
  estimatedTotalCostPercentageDifference: number;
}

export interface VariableImportanceResponse {
  technicalName: string;
  displayName: string;
  coefficient: number;
  absoluteCoefficient: number;
  importancePercentage: number;
  rank: number;
  direction: string;
  interpretation: string;
}

export interface SimilarProjectResponse {
  projectId: string;
  projectName: string;
  type: string;
  location: string;
  areaM2: number;
  durationMonths: number;
  baseCostCop: number;
  similarityPercentage: number;
  costDifferencePercentage: number;
  durationDifferencePercentage: number;
  createdAtUtc: string;
}

export interface ExecutiveDashboardResponse {
  projectId: string;
  projectName: string;
  estimatedTotalCostCop: number;
  riskLevel: string;
  riskDescription: string;
  cpi: number | null;
  spi: number | null;
  projectedDeviationCop: number;
  projectedDeviationPercentage: number;
  lastUpdatedUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  private get options() {
    const token = localStorage.getItem('jwt_token');
    return token ? { headers: new HttpHeaders({ Authorization: `Bearer ${token}` }) } : {};
  }

  private get blobOptions() {
    const token = localStorage.getItem('jwt_token');
    const headers = new HttpHeaders(token ? { Authorization: `Bearer ${token}` } : {});
    return { headers, observe: 'response' as const, responseType: 'blob' as const };
  }

  createProject(payload: CreateProjectRequest): Observable<CreateProjectResponse> {
    return this.http.post<CreateProjectResponse>(`${environment.apiBaseUrl}/projects`, payload, this.options);
  }

  createPredictionForProject(projectId: string, payload: CreatePredictionForProjectRequest): Observable<ProjectPredictionResponse> {
    return this.http.post<ProjectPredictionResponse>(`${environment.apiBaseUrl}/projects/${projectId}/predict`, payload, this.options);
  }

  createFinancialPredictionForProject(projectId: string): Observable<FinancialPredictionResponse> {
    return this.http.post<FinancialPredictionResponse>(`${environment.apiBaseUrl}/projects/${projectId}/financial-predict`, {}, this.options);
  }

  simulateProject(projectId: string, payload: SimulationRequest): Observable<SimulationResult> {
    return this.http.post<SimulationResult>(`${environment.apiBaseUrl}/projects/${projectId}/simulate`, payload, this.options);
  }

  getRecentProjects(take = 12): Observable<ProjectSummaryResponse[]> {
    return this.http.get<ProjectSummaryResponse[]>(`${environment.apiBaseUrl}/projects?take=${take}`, this.options);
  }

  getSimilarProjects(projectId: string): Observable<SimilarProjectResponse[]> {
    return this.http.get<SimilarProjectResponse[]>(`${environment.apiBaseUrl}/projects/${projectId}/similar`, this.options);
  }

  getProjectActionHistory(projectId: string): Observable<ProjectActionHistoryItem[]> {
    return this.http.get<ProjectActionHistoryItem[]>(`${environment.apiBaseUrl}/projects/${projectId}/history`, this.options);
  }

  getRecentPredictions(take = 8): Observable<PredictionHistoryResponse[]> {
    return this.http.get<PredictionHistoryResponse[]>(`${environment.apiBaseUrl}/predictions?take=${take}`, this.options);
  }

  getVariableImportance(): Observable<VariableImportanceResponse[]> {
    return this.http.get<VariableImportanceResponse[]>(`${environment.apiBaseUrl}/predictions/variable-importance`, this.options);
  }

  getRecentEvm(take = 8): Observable<EvmSummaryResponse[]> {
    return this.http.get<EvmSummaryResponse[]>(`${environment.apiBaseUrl}/evm/recent?take=${take}`, this.options);
  }

  getRecentFinancialPredictions(take = 8): Observable<FinancialPredictionSummaryResponse[]> {
    return this.http.get<FinancialPredictionSummaryResponse[]>(`${environment.apiBaseUrl}/financial-predictions?take=${take}`, this.options);
  }

  calculateEvm(projectId: string, periodDateUtc?: string): Observable<EvmCalculationResponse> {
    return this.http.post<EvmCalculationResponse>(`${environment.apiBaseUrl}/evm/calculate`, {
      projectId,
      periodDateUtc: periodDateUtc ?? null
    }, this.options);
  }

  getEvmHistory(projectId: string, take = 24): Observable<EvmHistoryPoint[]> {
    return this.http.get<EvmHistoryPoint[]>(`${environment.apiBaseUrl}/evm/${projectId}/history?take=${take}`, this.options);
  }

  downloadPredictionPdf(predictionId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/predictions/${predictionId}/pdf`, this.blobOptions);
  }

  downloadPredictionExcel(predictionId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/predictions/${predictionId}/excel`, this.blobOptions);
  }

  downloadEvmPdf(recordId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/evm/records/${recordId}/pdf`, this.blobOptions);
  }

  downloadEvmExcel(recordId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/evm/records/${recordId}/excel`, this.blobOptions);
  }

  getExecutiveDashboard(projectId: string): Observable<ExecutiveDashboardResponse> {
    return this.http.get<ExecutiveDashboardResponse>(`${environment.apiBaseUrl}/projects/${projectId}/executive-dashboard`, this.options);
  }

  downloadExecutiveDashboardPdf(projectId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/projects/${projectId}/executive-dashboard/pdf`, this.blobOptions);
  }
}
