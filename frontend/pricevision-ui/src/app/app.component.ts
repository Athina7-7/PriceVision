import { CommonModule, isPlatformBrowser } from '@angular/common';
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
  FinancialPredictionResponse,
  FinancialPredictionSummaryResponse,
  PredictionHistoryResponse,
  ProjectActionHistoryItem,
  ProjectPredictionResponse,
  ProjectSummaryResponse
  ,
  ProjectValidationWarningResponse
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

  activeSection: 'registro' | 'prediccion' | 'evm' | 'historial' = 'registro';

  loading = false;
  error = '';
  success = '';

  selectedProject: ProjectSummaryResponse | null = null;
  latest: EvmCalculationResponse | null = null;
  history: EvmHistoryPoint[] = [];
  prediction: ProjectPredictionResponse | null = null;
  actionHistory: ProjectActionHistoryItem[] = [];
  projects: ProjectSummaryResponse[] = [];
  recentPredictions: PredictionHistoryResponse[] = [];
  recentEvm: EvmSummaryResponse[] = [];
  selectedPredictionDetail: PredictionHistoryResponse | null = null;
  selectedFinancialDetail: FinancialPredictionSummaryResponse | null = null;
  selectedEvmDetail: EvmSummaryResponse | null = null;
  validationWarnings: ProjectValidationWarningResponse[] = [];
  recentFinancialPredictions: FinancialPredictionSummaryResponse[] = [];

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

  validationErrors: string[] = [];
  readonly projectTypes = ['Residencial', 'Comercial', 'Industrial', 'Remodelacion'];
  readonly locations = ['Bogota', 'Medellin', 'Cali', 'Barranquilla', 'Rural'];

  constructor(
    private readonly apiService: ApiService,
    @Inject(PLATFORM_ID) private readonly platformId: object
  ) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.loadProjects();
    this.loadRecentPredictions();
    this.loadRecentFinancialPredictions();
    this.loadRecentEvm();
  }

  registerProject(): void {
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

    if (this.predictionSelection.predictMaterials && project.hasMaterialsPrediction) {
      this.error = 'Ese proyecto ya tiene prediccion de materiales.';
      this.success = '';
      return;
    }

    if (this.predictionSelection.predictLabor && project.hasLaborPrediction) {
      this.error = 'Ese proyecto ya tiene prediccion de mano de obra.';
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
          predictionId: '',
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
          createdAtUtc: result.createdAtUtc
        };
        this.success = 'Prediccion generada correctamente.';
        this.loading = false;
        this.activeSection = 'prediccion';
        this.evmSelection.projectId = result.projectId;
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

    if (project.hasFinancialPrediction) {
      this.error = 'Ese proyecto ya tiene una prediccion financiera registrada.';
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

  viewProject(project: ProjectSummaryResponse): void {
    this.selectedProject = project;
    this.predictionSelection.projectId = project.projectId;
    this.evmSelection.projectId = project.projectId;
    this.prediction = null;
    this.latest = null;
    this.history = [];
    this.error = '';
    this.success = '';
    this.activeSection = 'historial';
    this.loadActionHistory(project.projectId, true);
    this.loadEvmHistory(project.projectId, false);
  }

  selectPredictionDetail(item: PredictionHistoryResponse): void {
    this.selectedPredictionDetail = item;
    this.predictionSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
  }

  selectEvmDetail(item: EvmSummaryResponse): void {
    this.selectedEvmDetail = item;
    this.evmSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
    this.loadEvmHistory(item.projectId);
  }

  selectFinancialDetail(item: FinancialPredictionSummaryResponse): void {
    this.selectedFinancialDetail = item;
    this.predictionSelection.projectId = item.projectId;
    this.selectedProject = this.projects.find((project) => project.projectId === item.projectId) ?? this.selectedProject;
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
}
