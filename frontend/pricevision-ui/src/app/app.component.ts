import { CommonModule, isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Component, Inject, OnInit, PLATFORM_ID } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import {
  ApiService,
  CreateProjectResponse,
  CreatePredictionForProjectRequest,
  CreateProjectRequest,
  EvmSummaryResponse,
  EvmCalculationResponse,
  EvmHistoryPoint,
  ExecutiveDashboardResponse,
  FinancialPredictionResponse,
  FinancialPredictionSummaryResponse,
  PredictionHistoryResponse,
  ProjectActionHistoryItem,
  ProjectPredictionResponse,
  ProjectSummaryResponse,
  ProjectValidationWarningResponse,
  SimulationResult,
  VariableImportanceResponse,
  SimilarProjectResponse
} from './core/services/api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'PriceVision';

  activeSection: 'login' | 'registro' | 'prediccion' | 'simulacion' | 'evm' | 'historial' | 'dashboard' = 'login';
  isAuthenticated = false;
  currentUserRole = '';

  loading = false;
  error = '';
  success = '';

  loginForm = { username: '', password: '' };
  selectedProject: ProjectSummaryResponse | null = null;
  latest: EvmCalculationResponse | null = null;
  history: EvmHistoryPoint[] = [];
  prediction: ProjectPredictionResponse | null = null;
  actionHistory: ProjectActionHistoryItem[] = [];
  projects: ProjectSummaryResponse[] = [];
  recentPredictions: PredictionHistoryResponse[] = [];
  variableImportance: VariableImportanceResponse[] = [];
  recentEvm: EvmSummaryResponse[] = [];
  selectedPredictionDetail: PredictionHistoryResponse | null = null;
  selectedFinancialDetail: FinancialPredictionSummaryResponse | null = null;
  selectedEvmDetail: EvmSummaryResponse | null = null;
  executiveDashboard: ExecutiveDashboardResponse | null = null;
  similarProjects: SimilarProjectResponse[] = [];
  loadingSimilar = false;
  simulationResult: SimulationResult | null = null;
  validationWarnings: ProjectValidationWarningResponse[] = [];
  recentFinancialPredictions: FinancialPredictionSummaryResponse[] = [];
  downloadingPredictionId = '';
  downloadingEvmId = '';
  downloadingDashboardId = '';
  downloadingPredictionExcelId = '';
  downloadingEvmExcelId = '';
  showPredictionChart = false;
  showEvmChart = false;
  
  historyFilter = {
    startDate: '',
    endDate: '',
    projectId: ''
  };
  financialHistory: FinancialPredictionSummaryResponse[] = [];

  form = {
    name: '',
    areaM2: null as number | null,
    location: '',
    type: '',
    durationMonths: null as number | null,
    baseCostCop: null as number | null
  };

  predictionSelection = {
    projectId: '',
    predictMaterials: true,
    predictLabor: true
  };

  evmSelection = {
    projectId: ''
  };

  simulationForm = {
    projectId: '',
    simulatedDurationMonths: null as number | null,
    simulatedBaseCostCop: null as number | null
  };

  predictionLocationFilter = '';
  evmLocationFilter = '';

  validationErrors: string[] = [];
  readonly projectTypes = ['Residencial', 'Comercial', 'Industrial', 'Remodelacion'];
  readonly locations = ['Bogota', 'Medellin', 'Cali', 'Barranquilla', 'Rural'];

  constructor(
    private readonly apiService: ApiService,
    private readonly http: HttpClient,
    @Inject(PLATFORM_ID) private readonly platformId: object
  ) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const token = localStorage.getItem('jwt_token');
    const role = localStorage.getItem('user_role');
    if (token) {
      this.isAuthenticated = true;
      this.currentUserRole = role ?? 'User';
      this.activeSection = 'registro';
      this.loadInitialData();
    }
  }

  login(): void {
    this.loading = true;
    this.error = '';
    this.http.post<any>('/api/auth/login', this.loginForm).subscribe({
      next: (res) => {
        localStorage.setItem('jwt_token', res.token);
        localStorage.setItem('user_role', res.role);
        this.isAuthenticated = true;
        this.currentUserRole = res.role;
        this.activeSection = 'registro';
        this.loading = false;
        this.loadInitialData();
      },
      error: () => {
        this.error = 'Credenciales invalidas.';
        this.loading = false;
      }
    });
  }

  logout(): void {
    localStorage.removeItem('jwt_token');
    localStorage.removeItem('user_role');
    this.isAuthenticated = false;
    this.currentUserRole = '';
    this.activeSection = 'login';
    this.loginForm = { username: '', password: '' };
  }

  private loadInitialData(): void {
    this.loadProjects();
    this.loadRecentPredictions();
    this.loadVariableImportance();
    this.loadRecentFinancialPredictions();
    this.loadRecentEvm();
    this.loadFinancialHistory();
  }

  hasRole(allowedRoles: string[]): boolean {
    return allowedRoles.includes(this.currentUserRole) || this.currentUserRole === 'Admin';
  }

  registerProject(): void {
    const token = isPlatformBrowser(this.platformId) ? localStorage.getItem('jwt_token') : null;
    if (!token) {
      this.error = 'Debes iniciar sesion para registrar un proyecto.';
      this.success = '';
      this.activeSection = 'login';
      return;
    }

    this.validationErrors = this.validateProjectForm();
    if (this.validationErrors.length > 0) {
      this.error = '';
      this.success = '';
      return;
    }

    this.loading = true;
    this.validationErrors = [];
    this.validationWarnings = [];
    this.error = '';
    this.success = '';

    const payload: CreateProjectRequest = {
      name: this.form.name.trim(),
      areaM2: this.form.areaM2 ?? 0,
      location: this.form.location.trim(),
      type: this.form.type.trim(),
      durationMonths: this.form.durationMonths ?? 0,
      baseCostCop: this.form.baseCostCop ?? 0
    };

    this.apiService.createProject(payload).subscribe({
      next: (response: CreateProjectResponse) => {
        const project = response.project;
        this.selectedProject = project;
        this.prediction = null;
        this.latest = null;
        this.history = [];
        this.actionHistory = [];
        this.validationWarnings = response.validationWarnings;
        this.predictionSelection.projectId = project.projectId;
        this.evmSelection.projectId = project.projectId;
        this.prepareSimulationForm(project);
        this.success = response.validationWarnings.length > 0
          ? 'Proyecto registrado con advertencias historicas.'
          : 'Proyecto registrado correctamente.';
        this.loading = false;
        this.activeSection = 'prediccion';
        this.loadProjects();
        this.resetForm();
      },
      error: (err) => {
        this.loading = false;
        if (err?.status === 401) {
          localStorage.removeItem('jwt_token');
          localStorage.removeItem('user_role');
          this.isAuthenticated = false;
          this.currentUserRole = '';
          this.activeSection = 'login';
          this.error = 'Sesion expirada o no autorizada. Inicia sesion de nuevo.';
          return;
        }
        this.error = err?.error?.error ?? 'No fue posible registrar el proyecto.';
      }
    });
  }

  createPrediction(): void {
    if (!this.predictionSelection.projectId) {
      this.error = 'Selecciona un proyecto para predecir.';
      this.success = '';
      return;
    }

    if (!this.predictionSelection.predictMaterials && !this.predictionSelection.predictLabor) {
      this.error = 'Selecciona al menos un modelo de prediccion.';
      this.success = '';
      return;
    }

    const project = this.projects.find((item) => item.projectId === this.predictionSelection.projectId);
    if (!project) {
      this.error = 'No se encontro el proyecto seleccionado.';
      this.success = '';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    const payload: CreatePredictionForProjectRequest = {
      predictMaterials: this.predictionSelection.predictMaterials,
      predictLabor: this.predictionSelection.predictLabor
    };

    this.apiService.createPredictionForProject(this.predictionSelection.projectId, payload).subscribe({
      next: (result) => {
        this.prediction = result;
        this.selectedPredictionDetail = {
          predictionId: result.predictionId,
          projectId: result.projectId,
          projectName: result.name,
          areaM2: result.areaM2,
          type: result.type,
          location: result.location,
          durationMonths: result.durationMonths,
          baseCostCop: result.baseCostCop,
          predictedMaterials: result.predictMaterials,
          predictedLabor: result.predictLabor,
          estimatedMaterialQuantity: result.materialesEstimados?.quantity ?? 0,
          estimatedMaterialCostCop: result.materialesEstimados?.costCop ?? 0,
          requiredLaborHours: result.manoObraRequeridaHorasPersona ?? 0,
          modelType: result.modelType,
          modelVersion: result.modelVersion,
          createdAtUtc: result.createdAtUtc
        };
        this.success = 'Prediccion generada correctamente.';
        this.loading = false;
        this.activeSection = 'prediccion';
        this.evmSelection.projectId = result.projectId;
        this.simulationForm.projectId = result.projectId;
        this.loadProjects(result.projectId);
        this.loadActionHistory(result.projectId);
        this.loadRecentPredictions();
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'No fue posible generar la prediccion.';
      }
    });
  }

  calculateEvm(): void {
    if (!this.evmSelection.projectId) {
      this.error = 'Selecciona un proyecto para calcular EVM.';
      this.success = '';
      return;
    }

    const project = this.projects.find((item) => item.projectId === this.evmSelection.projectId);
    if (!project) {
      this.error = 'No se encontro el proyecto seleccionado.';
      this.success = '';
      return;
    }

    if (!project.hasPrediction) {
      this.error = 'Ese proyecto aun no tiene prediccion.';
      this.success = '';
      return;
    }

    if (project.hasEvm) {
      this.error = 'Ese proyecto ya tiene un calculo EVM registrado.';
      this.success = '';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    this.apiService.calculateEvm(this.evmSelection.projectId).subscribe({
      next: (result) => {
        this.latest = result;
        this.selectedEvmDetail = {
          recordId: result.recordId,
          projectId: result.projectId,
          projectName: project.name,
          areaM2: project.areaM2,
          type: project.type,
          location: project.location,
          durationMonths: project.durationMonths,
          baseCostCop: project.baseCostCop,
          periodDateUtc: result.periodDateUtc,
          pv: result.pv,
          ev: result.ev,
          ac: result.ac,
          cpi: result.cpi,
          spi: result.spi,
          costInterpretation: result.costInterpretation,
          scheduleInterpretation: result.scheduleInterpretation,
          createdAtUtc: result.periodDateUtc
        };
        this.success = 'Calculo EVM guardado correctamente.';
        this.loading = false;
        this.activeSection = 'evm';
        this.loadProjects(result.projectId);
        this.loadEvmHistory(result.projectId);
        this.loadActionHistory(result.projectId);
        this.loadRecentEvm();
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'No fue posible calcular EVM.';
      }
    });
  }

  createFinancialPrediction(): void {
    if (!this.predictionSelection.projectId) {
      this.error = 'Selecciona un proyecto para generar la prediccion financiera.';
      this.success = '';
      return;
    }

    const project = this.projects.find((item) => item.projectId === this.predictionSelection.projectId);
    if (!project) {
      this.error = 'No se encontro el proyecto seleccionado.';
      this.success = '';
      return;
    }

    if (!project.hasMaterialsPrediction || !project.hasLaborPrediction) {
      this.error = 'El proyecto necesita predicciones de materiales y mano de obra antes de la prediccion financiera.';
      this.success = '';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    this.apiService.createFinancialPredictionForProject(project.projectId).subscribe({
      next: (result: FinancialPredictionResponse) => {
        this.selectedFinancialDetail = result;
        this.success = 'Prediccion financiera generada correctamente.';
        this.loading = false;
        this.activeSection = 'prediccion';
        this.loadProjects(result.projectId);
        this.loadActionHistory(result.projectId);
        this.loadRecentFinancialPredictions();
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'No fue posible generar la prediccion financiera.';
      }
    });
  }

  loadProjects(selectProjectId?: string): void {
    this.apiService.getRecentProjects(30).subscribe({
      next: (items) => {
        this.projects = items;
        const targetId = selectProjectId ?? this.selectedProject?.projectId ?? this.predictionSelection.projectId ?? this.evmSelection.projectId;
        if (targetId) {
          const match = items.find((item) => item.projectId === targetId) ?? null;
          this.selectedProject = match;
          if (match && !this.predictionSelection.projectId) {
            this.predictionSelection.projectId = match.projectId;
          }
          if (match && !this.evmSelection.projectId) {
            this.evmSelection.projectId = match.projectId;
          }
          if (match && !this.simulationForm.projectId) {
            this.prepareSimulationForm(match);
          }
        }
      },
      error: () => {
        this.projects = [];
      }
    });
  }

  loadRecentPredictions(): void {
    this.apiService.getRecentPredictions(8).subscribe({
      next: (items) => {
        this.recentPredictions = items;
        if (!this.selectedPredictionDetail && items.length > 0) {
          this.selectedPredictionDetail = items[0];
        }
      },
      error: () => {
        this.recentPredictions = [];
      }
    });
  }

  loadRecentFinancialPredictions(): void {
    this.apiService.getRecentFinancialPredictions(8).subscribe({
      next: (items) => {
        this.recentFinancialPredictions = items;
        if (!this.selectedFinancialDetail && items.length > 0) {
          this.selectedFinancialDetail = items[0];
        }
      },
      error: () => {
        this.recentFinancialPredictions = [];
      }
    });
  }

  loadRecentEvm(): void {
    this.apiService.getRecentEvm(8).subscribe({
      next: (items) => {
        this.recentEvm = items;
        if (!this.selectedEvmDetail && items.length > 0) {
          this.selectedEvmDetail = items[0];
        }
      },
      error: () => {
        this.recentEvm = [];
      }
    });
  }

  loadFinancialHistory(): void {
    this.loading = true;
    this.error = '';

    if (this.historyFilter.startDate && this.historyFilter.endDate) {
      if (new Date(this.historyFilter.startDate) > new Date(this.historyFilter.endDate)) {
        this.error = 'La fecha inicial no puede ser mayor a la fecha final.';
        this.loading = false;
        return;
      }
    }

    this.apiService.getFinancialPredictionHistory({
      startDate: this.historyFilter.startDate || undefined,
      endDate: this.historyFilter.endDate || undefined,
      projectId: this.historyFilter.projectId || undefined
    }).subscribe({
      next: (items) => {
        this.financialHistory = items;
        this.loading = false;
      },
      error: (err) => {
        this.error = err?.error?.error ?? 'Error al cargar el historial financiero.';
        this.financialHistory = [];
        this.loading = false;
      }
    });
  }

  clearHistoryFilter(): void {
    this.historyFilter = { startDate: '', endDate: '', projectId: '' };
    this.loadFinancialHistory();
  }

  viewProject(project: ProjectSummaryResponse): void {
    this.selectedProject = project;
    this.predictionSelection.projectId = project.projectId;
    this.evmSelection.projectId = project.projectId;
    this.prepareSimulationForm(project);
    this.prediction = null;
    this.latest = null;
    this.history = [];
    this.error = '';
    this.success = '';
    this.activeSection = 'historial';
    this.loadActionHistory(project.projectId, true);
    this.loadEvmHistory(project.projectId, false);
    this.loadSimilarProjects(project.projectId);
  }

  selectPredictionDetail(item: PredictionHistoryResponse): void {
    this.selectedPredictionDetail = item;
    this.predictionSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
    if (this.selectedProject) {
      this.prepareSimulationForm(this.selectedProject);
    }
    this.showPredictionChart = false;
  }

  loadVariableImportance(): void {
    this.apiService.getVariableImportance().subscribe({
      next: (items) => {
        this.variableImportance = items;
      },
      error: () => {
        this.variableImportance = [];
      }
    });
  }

  selectEvmDetail(item: EvmSummaryResponse): void {
    this.selectedEvmDetail = item;
    this.evmSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
    this.showEvmChart = false;
    this.loadEvmHistory(item.projectId);
  }

  selectFinancialDetail(item: FinancialPredictionSummaryResponse): void {
    this.selectedFinancialDetail = item;
    this.predictionSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
    if (this.selectedProject) {
      this.prepareSimulationForm(this.selectedProject);
    }
  }

  runSimulation(): void {
    const simulatedDurationMonths = Number(this.simulationForm.simulatedDurationMonths);
    const simulatedBaseCostCop = Number(this.simulationForm.simulatedBaseCostCop);

    if (!this.simulationForm.projectId) {
      this.error = 'Selecciona un proyecto para simular.';
      this.success = '';
      return;
    }

    if (!Number.isFinite(simulatedDurationMonths) || simulatedDurationMonths <= 0) {
      this.error = 'La duracion simulada debe ser mayor que cero.';
      this.success = '';
      return;
    }

    if (!Number.isFinite(simulatedBaseCostCop) || simulatedBaseCostCop < 0) {
      this.error = 'El costo base simulado debe ser mayor o igual que cero.';
      this.success = '';
      return;
    }

    this.loading = true;
    this.error = '';
    this.success = '';

    this.apiService.simulateProject(this.simulationForm.projectId, {
      simulatedDurationMonths,
      simulatedBaseCostCop
    }).subscribe({
      next: (result) => {
        this.simulationResult = result;
        this.selectedProject = this.projects.find((project) => project.projectId === result.projectId) ?? this.selectedProject;
        this.loading = false;
        this.success = 'Simulacion generada sin alterar los datos originales.';
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'No fue posible ejecutar la simulacion.';
      }
    });
  }

  onSimulationProjectChange(): void {
    const project = this.projects.find((item) => item.projectId === this.simulationForm.projectId);
    if (project) {
      this.prepareSimulationForm(project);
    }
  }

  downloadPredictionPdf(predictionId: string, fallbackName: string, event?: Event): void {
    event?.stopPropagation();

    if (!predictionId) {
      this.error = 'No se encontro el identificador de la prediccion para descargar.';
      this.success = '';
      return;
    }

    this.downloadingPredictionId = predictionId;
    this.error = '';

    this.apiService.downloadPredictionPdf(predictionId).subscribe({
      next: (response) => {
        this.saveFileResponse(response, fallbackName);
        this.success = 'PDF de prediccion descargado correctamente.';
        this.downloadingPredictionId = '';
      },
      error: (err) => {
        this.downloadingPredictionId = '';
        this.error = err?.error?.error ?? 'No fue posible descargar el PDF de prediccion.';
      }
    });
  }

  downloadPredictionExcel(predictionId: string, fallbackName: string, event?: Event): void {
    event?.stopPropagation();

    if (!predictionId) {
      this.error = 'No se encontro el identificador de la prediccion para descargar.';
      this.success = '';
      return;
    }

    this.downloadingPredictionExcelId = predictionId;
    this.error = '';

    this.apiService.downloadPredictionExcel(predictionId).subscribe({
      next: (response) => {
        this.saveFileResponse(response, fallbackName);
        this.success = 'Excel de prediccion descargado correctamente.';
        this.downloadingPredictionExcelId = '';
      },
      error: (err) => {
        this.downloadingPredictionExcelId = '';
        this.error = err?.error?.error ?? 'No fue posible descargar el Excel de prediccion.';
      }
    });
  }

  downloadEvmPdf(recordId: string, fallbackName: string, event?: Event): void {
    event?.stopPropagation();

    if (!recordId) {
      this.error = 'No se encontro el identificador del registro EVM para descargar.';
      this.success = '';
      return;
    }

    this.downloadingEvmId = recordId;
    this.error = '';

    this.apiService.downloadEvmPdf(recordId).subscribe({
      next: (response) => {
        this.saveFileResponse(response, fallbackName);
        this.success = 'PDF EVM descargado correctamente.';
        this.downloadingEvmId = '';
      },
      error: (err) => {
        this.downloadingEvmId = '';
        this.error = err?.error?.error ?? 'No fue posible descargar el PDF EVM.';
      }
    });
  }

  downloadEvmExcel(recordId: string, fallbackName: string, event?: Event): void {
    event?.stopPropagation();

    if (!recordId) {
      this.error = 'No se encontro el identificador del registro EVM para descargar.';
      this.success = '';
      return;
    }

    this.downloadingEvmExcelId = recordId;
    this.error = '';

    this.apiService.downloadEvmExcel(recordId).subscribe({
      next: (response) => {
        this.saveFileResponse(response, fallbackName);
        this.success = 'Excel EVM descargado correctamente.';
        this.downloadingEvmExcelId = '';
      },
      error: (err) => {
        this.downloadingEvmExcelId = '';
        this.error = err?.error?.error ?? 'No fue posible descargar el Excel EVM.';
      }
    });
  }

  loadExecutiveDashboard(): void {
    if (!this.selectedProject) {
      this.error = 'Selecciona un proyecto para ver el dashboard.';
      this.success = '';
      return;
    }
    this.loading = true;
    this.error = '';
    this.success = '';
    this.apiService.getExecutiveDashboard(this.selectedProject.projectId).subscribe({
      next: (result: ExecutiveDashboardResponse) => {
        this.executiveDashboard = result;
        this.loading = false;
        this.activeSection = 'dashboard';
      },
      error: (err: any) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'No fue posible cargar el dashboard ejecutivo.';
      }
    });
  }

  downloadDashboardPdf(projectId: string, fallbackName: string, event?: Event): void {
    event?.stopPropagation();
    if (!projectId) return;
    this.downloadingDashboardId = projectId;
    this.error = '';
    this.apiService.downloadExecutiveDashboardPdf(projectId).subscribe({
      next: (response: HttpResponse<Blob>) => {
        this.saveFileResponse(response, fallbackName);
        this.success = 'Dashboard ejecutivo exportado correctamente.';
        this.downloadingDashboardId = '';
      },
      error: (err: any) => {
        this.downloadingDashboardId = '';
        this.error = err?.error?.error ?? 'No fue posible descargar el PDF del dashboard.';
      }
    });
  }

  isDashboardDownloading(projectId: string): boolean {
    return !!projectId && this.downloadingDashboardId === projectId;
  }

  isPredictionDownloading(predictionId: string): boolean {
    return !!predictionId && this.downloadingPredictionId === predictionId;
  }

  isPredictionExcelDownloading(predictionId: string): boolean {
    return !!predictionId && this.downloadingPredictionExcelId === predictionId;
  }

  isEvmDownloading(recordId: string): boolean {
    return !!recordId && this.downloadingEvmId === recordId;
  }

  isEvmExcelDownloading(recordId: string): boolean {
    return !!recordId && this.downloadingEvmExcelId === recordId;
  }

  togglePredictionChart(event?: Event): void {
    event?.stopPropagation();
    this.showPredictionChart = !this.showPredictionChart;
  }

  toggleEvmChart(event?: Event): void {
    event?.stopPropagation();
    this.showEvmChart = !this.showEvmChart;
  }

  predictionCostComparisonGeometry(width = 860, height = 260): ComparisonGeometry {
    return this.buildComparisonGeometry(
      this.selectedPredictionDetail?.baseCostCop ?? 0,
      this.predictionEstimatedCost,
      width,
      height
    );
  }

  evmCostComparisonGeometry(width = 860, height = 260): ComparisonGeometry {
    return this.buildComparisonGeometry(
      this.selectedEvmDetail?.pv ?? 0,
      this.selectedEvmDetail?.ac ?? 0,
      width,
      height
    );
  }

  simulationComparisonGeometry(width = 860, height = 260): ComparisonGeometry {
    return this.buildComparisonGeometry(
      this.simulationResult?.originalEstimatedTotalCostCop ?? 0,
      this.simulationResult?.simulatedEstimatedTotalCostCop ?? 0,
      width,
      height
    );
  }

  simulationMetricBarWidth(originalValue: number, simulatedValue: number, target: 'original' | 'simulated'): number {
    const maxValue = Math.max(Math.abs(originalValue), Math.abs(simulatedValue), 1);
    const value = target === 'original' ? Math.abs(originalValue) : Math.abs(simulatedValue);
    return Math.max(4, (value / maxValue) * 100);
  }

  simulationMetricPercentage(originalValue: number, simulatedValue: number): number {
    const original = Number(originalValue);
    const simulated = Number(simulatedValue);

    if (!Number.isFinite(original) || !Number.isFinite(simulated)) {
      return 0;
    }

    if (original === 0) {
      return simulated === 0 ? 0 : 100;
    }

    return ((simulated - original) / original) * 100;
  }

  simulationTotalCostPercentage(result: SimulationResult): number {
    const totalCostMetric = result.metrics.find((metric) => metric.label === 'Costo estimado total (COP)');
    if (totalCostMetric) {
      return this.simulationMetricPercentage(totalCostMetric.originalValue, totalCostMetric.simulatedValue);
    }

    return this.simulationMetricPercentage(result.originalEstimatedTotalCostCop, result.simulatedEstimatedTotalCostCop);
  }

  get predictionEstimatedCost(): number {
    if (this.selectedFinancialDetail && this.selectedPredictionDetail?.projectId === this.selectedFinancialDetail.projectId) {
      return this.selectedFinancialDetail.estimatedTotalCostCop;
    }

    return this.selectedPredictionDetail?.estimatedMaterialCostCop ?? 0;
  }

  get predictionDeviationPercentage(): number {
    return this.calculateDeviationPercentage(
      this.selectedPredictionDetail?.baseCostCop ?? 0,
      this.predictionEstimatedCost
    );
  }

  get evmDeviationPercentage(): number {
    return this.calculateDeviationPercentage(
      this.selectedEvmDetail?.pv ?? 0,
      this.selectedEvmDetail?.ac ?? 0
    );
  }

  get filteredPredictionProjects(): ProjectSummaryResponse[] {
    return this.filterByLocation(this.projects, this.predictionLocationFilter);
  }

  get filteredRecentPredictions(): PredictionHistoryResponse[] {
    return this.filterByLocation(this.recentPredictions, this.predictionLocationFilter);
  }

  get filteredRecentFinancialPredictions(): FinancialPredictionSummaryResponse[] {
    return this.filterByLocation(this.recentFinancialPredictions, this.predictionLocationFilter);
  }

  get filteredEvmProjects(): ProjectSummaryResponse[] {
    return this.filterByLocation(this.projects, this.evmLocationFilter);
  }

  get filteredRecentEvm(): EvmSummaryResponse[] {
    return this.filterByLocation(this.recentEvm, this.evmLocationFilter);
  }

  onPredictionLocationFilterChange(): void {
    if (this.predictionSelection.projectId) {
      const selectedProjectVisible = this.filteredPredictionProjects.some(
        (item) => item.projectId === this.predictionSelection.projectId
      );

      if (!selectedProjectVisible) {
        this.predictionSelection.projectId = '';
      }
    }

    if (this.selectedPredictionDetail && this.selectedPredictionDetail.location !== this.predictionLocationFilter && this.predictionLocationFilter) {
      this.selectedPredictionDetail = this.filteredRecentPredictions[0] ?? null;
    }

    if (this.selectedFinancialDetail && this.selectedFinancialDetail.location !== this.predictionLocationFilter && this.predictionLocationFilter) {
      this.selectedFinancialDetail = this.filteredRecentFinancialPredictions[0] ?? null;
    }
  }

  onEvmLocationFilterChange(): void {
    if (this.evmSelection.projectId) {
      const selectedProjectVisible = this.filteredEvmProjects.some(
        (item) => item.projectId === this.evmSelection.projectId
      );

      if (!selectedProjectVisible) {
        this.evmSelection.projectId = '';
      }
    }

    if (this.selectedEvmDetail && this.selectedEvmDetail.location !== this.evmLocationFilter && this.evmLocationFilter) {
      this.selectedEvmDetail = this.filteredRecentEvm[0] ?? null;
    }
  }

  loadActionHistory(projectId: string, showLoading = false): void {
    if (showLoading) {
      this.loading = true;
      this.error = '';
    }

    this.apiService.getProjectActionHistory(projectId).subscribe({
      next: (items) => {
        this.actionHistory = items;
        if (showLoading) {
          this.loading = false;
        }
      },
      error: (err) => {
        if (showLoading) {
          this.loading = false;
        }
        this.actionHistory = [];
        this.error = err?.error?.error ?? 'No fue posible cargar el historial.';
      }
    });
  }

  loadEvmHistory(projectId: string, showLoading = false): void {
    if (showLoading) {
      this.loading = true;
      this.error = '';
    }

    this.apiService.getEvmHistory(projectId, 24).subscribe({
      next: (items) => {
        this.history = items;
        this.latest = items.length > 0
          ? {
              recordId: '',
              projectId,
              periodDateUtc: items[items.length - 1].periodDateUtc,
              pv: items[items.length - 1].pv,
              ev: items[items.length - 1].ev,
              ac: items[items.length - 1].ac,
              cpi: items[items.length - 1].cpi,
              spi: items[items.length - 1].spi,
              costInterpretation: items[items.length - 1].costInterpretation,
              scheduleInterpretation: items[items.length - 1].scheduleInterpretation
            }
          : null;
        if (showLoading) {
          this.loading = false;
        }
      },
      error: () => {
        this.history = [];
        this.latest = null;
        if (showLoading) {
          this.loading = false;
        }
      }
    });
  }

  loadSimilarProjects(projectId: string): void {
    this.loadingSimilar = true;
    this.apiService.getSimilarProjects(projectId).subscribe({
      next: (items) => {
        this.similarProjects = items;
        this.loadingSimilar = false;
      },
      error: () => {
        this.similarProjects = [];
        this.loadingSimilar = false;
      }
    });
  }

  evmPoint(series: 'pv' | 'ev' | 'ac', width = 860, height = 260): string {
    const values = this.history.map((x) => x[series]);
    if (values.length < 2) {
      return '';
    }

    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = Math.max(1, max - min);

    return values
      .map((value, index) => {
        const x = (index / (values.length - 1)) * width;
        const y = height - ((value - min) / span) * height;
        return `${x.toFixed(2)},${y.toFixed(2)}`;
      })
      .join(' ');
  }

  formatCop(value: number): string {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(value);
  }

  asDate(value: string): string {
    return new Date(value).toLocaleDateString('es-CO');
  }

  asDateTime(value: string): string {
    return new Date(value).toLocaleString('es-CO');
  }

  formatMetricValue(label: string, value: number): string {
    if (label.includes('COP')) {
      return this.formatCop(value);
    }

    return new Intl.NumberFormat('es-CO', {
      maximumFractionDigits: 2
    }).format(value);
  }

  financialConfidenceExplanation(item: FinancialPredictionSummaryResponse | FinancialPredictionResponse): string {
    return item.confidenceExplanation || 'El nivel de confianza indica que, considerando el error estandar del modelo, es probable que el valor real se ubique dentro del intervalo estimado.';
  }

  financialConfidenceLower(item: FinancialPredictionSummaryResponse | FinancialPredictionResponse): number {
    return item.confidenceIntervalLower || item.minimumEstimatedCostCop;
  }

  financialConfidenceUpper(item: FinancialPredictionSummaryResponse | FinancialPredictionResponse): number {
    return item.confidenceIntervalUpper || item.maximumEstimatedCostCop;
  }

  pdfFileName(location: string, projectName: string): string {
    return this.reportFileName(location, projectName, 'pdf');
  }

  excelFileName(location: string, projectName: string): string {
    return this.reportFileName(location, projectName, 'xlsx');
  }

  private reportFileName(location: string, projectName: string, extension: string): string {
    const cleanLocation = this.sanitizeFileNamePart(location);
    const cleanProjectName = this.sanitizeFileNamePart(projectName);
    const parts = [cleanLocation, cleanProjectName].filter((part) => part.length > 0);
    const baseName = parts.length > 0 ? parts.join(' - ') : 'reporte';
    return `${baseName}.${extension}`;
  }

  private validateProjectForm(): string[] {
    const errors: string[] = [];

    if (!this.form.name.trim()) {
      errors.push('El nombre del proyecto es obligatorio.');
    }

    if (this.form.areaM2 === null || this.form.areaM2 <= 0) {
      errors.push('El area debe ser mayor que cero.');
    }

    if (!this.form.location.trim()) {
      errors.push('La ubicacion es obligatoria.');
    }

    if (!this.form.type.trim()) {
      errors.push('El tipo de proyecto es obligatorio.');
    }

    if (this.form.durationMonths === null || this.form.durationMonths <= 0) {
      errors.push('La duracion estimada debe ser mayor que cero.');
    }

    if (this.form.baseCostCop === null || this.form.baseCostCop < 0) {
      errors.push('Los costos base deben ser cero o mayores.');
    }

    return errors;
  }

  private resetForm(): void {
    this.form = {
      name: '',
      areaM2: null,
      location: '',
      type: '',
      durationMonths: null,
      baseCostCop: null
    };
  }

  private prepareSimulationForm(project: ProjectSummaryResponse): void {
    this.simulationForm = {
      projectId: project.projectId,
      simulatedDurationMonths: project.durationMonths,
      simulatedBaseCostCop: project.baseCostCop
    };
    this.simulationResult = null;
  }

  private saveFileResponse(response: HttpResponse<Blob>, fallbackName: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const file = response.body;
    if (!file) {
      this.error = 'La respuesta del servidor no incluyo el archivo solicitado.';
      this.success = '';
      return;
    }

    const fileName = fallbackName;
    const objectUrl = URL.createObjectURL(file);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  }

  private sanitizeFileNamePart(value: string): string {
    return value
      .replace(/[<>:"/\\|?*\u0000-\u001F]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .replace(/[.\- ]+$/g, '');
  }

  private filterByLocation<T extends { location: string }>(items: T[], location: string): T[] {
    if (!location) {
      return items;
    }

    return items.filter((item) => item.location === location);
  }

  private buildComparisonGeometry(planned: number, estimated: number, width: number, height: number): ComparisonGeometry {
    const paddingX = 56;
    const paddingY = 28;
    const chartWidth = width - paddingX * 2;
    const chartHeight = height - paddingY * 2;
    const xStart = paddingX;
    const xEnd = paddingX + chartWidth;
    const maxValue = Math.max(planned, estimated, 1);
    const minValue = Math.min(planned, estimated, 0);
    const range = Math.max(maxValue - minValue, 1);

    const yFor = (value: number) => paddingY + chartHeight - ((value - minValue) / range) * chartHeight;
    const plannedY = yFor(planned);
    const estimatedY = yFor(estimated);
    const deltaMidY = ((plannedY + estimatedY) / 2).toFixed(2);
    const deltaLabelY = Math.max(24, Math.min(height - 36, Number(deltaMidY) - 12));

    return {
      plannedLine: `${xStart},${plannedY.toFixed(2)} ${xEnd},${plannedY.toFixed(2)}`,
      estimatedLine: `${xStart},${plannedY.toFixed(2)} ${xEnd},${estimatedY.toFixed(2)}`,
      varianceArea: `${xStart},${plannedY.toFixed(2)} ${xEnd},${plannedY.toFixed(2)} ${xEnd},${estimatedY.toFixed(2)}`,
      leftX: xStart,
      rightX: xEnd,
      plannedY,
      estimatedY,
      labelX: xEnd - 110,
      labelY: deltaLabelY
    };
  }

  private calculateDeviationPercentage(planned: number, estimated: number): number {
    if (!planned) {
      return 0;
    }

    return ((estimated - planned) / planned) * 100;
  }
}

interface ComparisonGeometry {
  plannedLine: string;
  estimatedLine: string;
  varianceArea: string;
  leftX: number;
  rightX: number;
  plannedY: number;
  estimatedY: number;
  labelX: number;
  labelY: number;
}
