import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
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
  historicalAverageCostPerM2Cop: number;
  locationTrendFactor: number;
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
  historicalAverageCostPerM2Cop: number;
  locationTrendFactor: number;
  createdAtUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  createProject(payload: CreateProjectRequest): Observable<CreateProjectResponse> {
    return this.http.post<CreateProjectResponse>(`${environment.apiBaseUrl}/projects`, payload);
  }

  createPredictionForProject(projectId: string, payload: CreatePredictionForProjectRequest): Observable<ProjectPredictionResponse> {
    return this.http.post<ProjectPredictionResponse>(`${environment.apiBaseUrl}/projects/${projectId}/predict`, payload);
  }

  createFinancialPredictionForProject(projectId: string): Observable<FinancialPredictionResponse> {
    return this.http.post<FinancialPredictionResponse>(`${environment.apiBaseUrl}/projects/${projectId}/financial-predict`, {});
  }

  getRecentProjects(take = 12): Observable<ProjectSummaryResponse[]> {
    return this.http.get<ProjectSummaryResponse[]>(`${environment.apiBaseUrl}/projects?take=${take}`);
  }

  getProjectActionHistory(projectId: string): Observable<ProjectActionHistoryItem[]> {
    return this.http.get<ProjectActionHistoryItem[]>(`${environment.apiBaseUrl}/projects/${projectId}/history`);
  }

  getRecentPredictions(take = 8): Observable<PredictionHistoryResponse[]> {
    return this.http.get<PredictionHistoryResponse[]>(`${environment.apiBaseUrl}/predictions?take=${take}`);
  }

  getRecentEvm(take = 8): Observable<EvmSummaryResponse[]> {
    return this.http.get<EvmSummaryResponse[]>(`${environment.apiBaseUrl}/evm/recent?take=${take}`);
  }

  getRecentFinancialPredictions(take = 8): Observable<FinancialPredictionSummaryResponse[]> {
    return this.http.get<FinancialPredictionSummaryResponse[]>(`${environment.apiBaseUrl}/financial-predictions?take=${take}`);
  }

  calculateEvm(projectId: string, periodDateUtc?: string): Observable<EvmCalculationResponse> {
    return this.http.post<EvmCalculationResponse>(`${environment.apiBaseUrl}/evm/calculate`, {
      projectId,
      periodDateUtc: periodDateUtc ?? null
    });
  }

  getEvmHistory(projectId: string, take = 24): Observable<EvmHistoryPoint[]> {
    return this.http.get<EvmHistoryPoint[]>(`${environment.apiBaseUrl}/evm/${projectId}/history?take=${take}`);
  }

  downloadPredictionPdf(predictionId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/predictions/${predictionId}/pdf`, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  downloadPredictionExcel(predictionId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/predictions/${predictionId}/excel`, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  downloadEvmPdf(recordId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/evm/records/${recordId}/pdf`, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  downloadEvmExcel(recordId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/evm/records/${recordId}/excel`, {
      observe: 'response',
      responseType: 'blob'
    });
  }
}
