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
            getRecentEvm: () => of([]),
            getProjectActionHistory: () => of([]),
            createProject: () => of(null),
            createPredictionForProject: () => of(null),
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
});
