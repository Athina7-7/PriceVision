import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { ApiService } from './core/services/api.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        {
          provide: ApiService,
          useValue: {
            getRecentProjects: () => of([]),
            getRecentPredictions: () => of([]),
            getVariableImportance: () => of([
              {
                technicalName: 'AreaM2',
                displayName: 'Area construida',
                coefficient: 1,
                absoluteCoefficient: 1,
                importancePercentage: 56.18,
                rank: 1,
                direction: 'Positiva',
                interpretation: 'Mayor area aumenta el costo estimado.'
              }
            ]),
            getRecentFinancialPredictions: () => of([]),
            getRecentEvm: () => of([]),
            getProjectActionHistory: () => of([]),
            createProject: () => of(null),
            createPredictionForProject: () => of(null),
            createFinancialPredictionForProject: () => of(null),
            simulateProject: () => of({
              projectId: 'project-1',
              projectName: 'Proyecto prueba',
              simulatedAtUtc: '2026-05-14T00:00:00Z',
              metrics: [],
              originalEstimatedTotalCostCop: 100,
              simulatedEstimatedTotalCostCop: 120,
              estimatedTotalCostDifferenceCop: 20,
              estimatedTotalCostPercentageDifference: 20
            }),
            calculateEvm: () => of(null),
            getEvmHistory: () => of([])
          }
        }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it("should have the 'PriceVision' title", () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('PriceVision');
  });

  it('should reject simulation with invalid duration', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.simulationForm = {
      projectId: 'project-1',
      simulatedDurationMonths: 0,
      simulatedBaseCostCop: 100
    };

    app.runSimulation();

    expect(app.error).toContain('duracion simulada');
    expect(app.simulationResult).toBeNull();
  });

  it('should run simulation without changing selected project values', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.selectedProject = {
      projectId: 'project-1',
      name: 'Proyecto prueba',
      areaM2: 100,
      location: 'Bogota',
      type: 'Residencial',
      durationMonths: 8,
      baseCostCop: 100,
      createdAtUtc: '2026-05-14T00:00:00Z',
      hasPrediction: false,
      hasMaterialsPrediction: false,
      hasLaborPrediction: false,
      hasFinancialPrediction: false,
      hasEvm: false
    };
    app.projects = [app.selectedProject];
    app.simulationForm = {
      projectId: 'project-1',
      simulatedDurationMonths: 10,
      simulatedBaseCostCop: 120
    };

    app.runSimulation();

    expect(app.selectedProject.durationMonths).toBe(8);
    expect(app.selectedProject.baseCostCop).toBe(100);
    expect(app.simulationResult?.simulatedEstimatedTotalCostCop).toBe(120);
  });

  it('should render statistical confidence tooltip for financial prediction detail', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.activeSection = 'prediccion';
    app.selectedFinancialDetail = {
      financialPredictionId: 'financial-1',
      projectId: 'project-1',
      projectName: 'Proyecto prueba',
      areaM2: 100,
      type: 'Residencial',
      location: 'Bogota',
      durationMonths: 8,
      baseCostCop: 1000,
      estimatedTotalCostCop: 1200,
      minimumEstimatedCostCop: 1004,
      maximumEstimatedCostCop: 1396,
      confidencePercentage: 95,
      confidenceLevel: '95%',
      standardError: 100,
      confidenceIntervalLower: 1004,
      confidenceIntervalUpper: 1396,
      confidenceExplanation: 'El nivel de confianza indica que el valor real puede ubicarse dentro del intervalo estimado.',
      historicalAverageCostPerM2Cop: 10,
      locationTrendFactor: 1,
      modelType: 'FinancialForecast',
      modelVersion: 'v1.0.0',
      createdAtUtc: '2026-05-14T00:00:00Z'
    };

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const tooltip = compiled.querySelector('.tooltip-trigger');
    expect(compiled.textContent).toContain('95');
    expect(compiled.textContent).toContain('Intervalo estimado');
    expect(tooltip?.getAttribute('title')).toContain('El nivel de confianza indica');
  });

  it('should render variable importance ranking from backend data', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.activeSection = 'prediccion';
    app.variableImportance = [
      {
        technicalName: 'AreaM2',
        displayName: 'Area construida',
        coefficient: 1,
        absoluteCoefficient: 1,
        importancePercentage: 56.18,
        rank: 1,
        direction: 'Positiva',
        interpretation: 'Mayor area aumenta el costo estimado.'
      }
    ];

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Importancia de variables');
    expect(compiled.textContent).toContain('Area construida');
    expect(compiled.textContent).toContain('56.18');
  });
});
